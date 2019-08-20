using System;
using System.Linq;
using System.Web.Http;
using System.Web.Routing;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Pipelines;

using Sitecore.XA.Foundation.Multisite.Model;
using Sitecore.XA.Foundation.Multisite.Pipelines.RefreshHttpRoutes;
using Sitecore.XA.Foundation.SitecoreExtensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Session;
using System.Web.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.XA.Foundation.Multisite;

namespace Sitecore.Support.XA.Foundation.Multisite.EventHandlers
{
    public class HttpRoutesRefresher
    {
        public void OnItemSaved(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");

            Item item = Event.ExtractParameter(args, 0) as Item;
            if (item == null || !item.TemplateID.Equals(Sitecore.XA.Foundation.Multisite.Templates.SiteDefinition.ID) || JobsHelper.IsPublishing())
            {
                return;
            }

            ClearRoutes(sender, args);
            PopulateRoutes();
        }

        public void OnItemSavedRemote(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");

            ItemSavedRemoteEventArgs eventArgs = args as ItemSavedRemoteEventArgs;
            if (eventArgs == null || eventArgs.Item == null || !eventArgs.Item.TemplateID.Equals(Sitecore.XA.Foundation.Multisite.Templates.SiteDefinition.ID) || JobsHelper.IsPublishing())
            {
                return;
            }

            ClearRoutes(sender, args);
            PopulateRoutes();
        }

        private void PopulateRoutes()
        {
            foreach (string virtualFolder in Sitecore.DependencyInjection.ServiceLocator.ServiceProvider.GetService<ISiteInfoResolver>().Sites.Select(s => s.VirtualFolder.Trim('/')).Distinct())
            {
                string route = virtualFolder.Length > 0 ? virtualFolder + "/" : virtualFolder;

                RefreshHttpRoutesArgs pipelineArgs = new RefreshHttpRoutesArgs(route);
                CorePipeline.Run("refreshHttpRoutes", pipelineArgs);

                foreach (HttpRouteDetails routeDetails in pipelineArgs.RoutesToRefresh)
                {
                    if (routeDetails.IsHttp)
                    {
                        RouteTable.Routes.MapHttpRoute(routeDetails.Name, routeDetails.RouteTemplate, routeDetails.Defaults);
                    }
                    else
                    {
                        RouteTable.Routes.MapRoute(routeDetails.Name, routeDetails.RouteTemplate, routeDetails.Defaults);
                    }

                }

                RouteTable.Routes.MapHttpRoute(route + "sxa", route + "sxa/{controller}/{action}").RouteHandler = new SessionHttpControllerRouteHandler();
            }
        }

        private void ClearRoutes(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");

            Log.Info("HttpRoutesRefresher clearing routes.", this);

            RouteBase[] routes = RouteTable.Routes.Where(route => ((Route)route).Url.Contains("sxa/")).ToArray();
            RouteCollection registeredRoutes = RouteTable.Routes;
            using (registeredRoutes.GetWriteLock())
            {
                foreach (RouteBase route in routes)
                {
                    registeredRoutes.Remove(route);
                }
            }

            Log.Info("HttpRoutesRefresher done.", this);
        }
    }
}
namespace Sitecore.Support.XA.Foundation.Multisite.EventHandlers
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data.Events;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Events;
    using Sitecore.Pipelines;
    using Sitecore.Web;
    using Sitecore.XA.Foundation.Multisite;
    using Sitecore.XA.Foundation.Multisite.Model;
    using Sitecore.XA.Foundation.Multisite.Pipelines.RefreshHttpRoutes;
    using Sitecore.XA.Foundation.SitecoreExtensions;
    using Sitecore.XA.Foundation.SitecoreExtensions.Session;
    using System;
    using System.Linq;
    using System.Web.Http;
    using System.Web.Mvc;
    using System.Web.Routing;

    public class HttpRoutesRefresher
    {
        public void OnItemSaved(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Item item = Event.ExtractParameter(args, 0) as Item;
            if (item != null && item.TemplateID.Equals(Sitecore.XA.Foundation.Multisite.Templates.SiteDefinition.ID) &&
                !JobsHelper.IsPublishing())
            {
                ClearRoutes(sender, args);
                PopulateRoutes();
            }
        }

        public void OnItemSavedRemote(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            ItemSavedRemoteEventArgs itemSavedRemoteEventArgs = args as ItemSavedRemoteEventArgs;
            if (itemSavedRemoteEventArgs != null && itemSavedRemoteEventArgs.Item != null &&
                itemSavedRemoteEventArgs.Item.TemplateID.Equals(Sitecore.XA.Foundation.Multisite.Templates
                    .SiteDefinition.ID) && !JobsHelper.IsPublishing())
            {
                ClearRoutes(sender, args);
                PopulateRoutes();
            }
        }

        private void PopulateRoutes()
        {
            foreach (string item in (from s in ServiceLocator.ServiceProvider.GetService<ISiteInfoResolver>().Sites
                                     select s.VirtualFolder.Trim('/')).Distinct())
            {
                string text = (item.Length > 0) ? (item + "/") : item;
                RefreshHttpRoutesArgs refreshHttpRoutesArgs = new RefreshHttpRoutesArgs(text);
                CorePipeline.Run("refreshHttpRoutes", refreshHttpRoutesArgs);
                RouteCollection routes = RouteTable.Routes;
                using (routes.GetWriteLock())
                {
                    foreach (HttpRouteDetails routeDetails in refreshHttpRoutesArgs.RoutesToRefresh)
                    {
                        if (routeDetails.IsHttp)
                        {
                            routes.MapHttpRoute(routeDetails.Name, routeDetails.RouteTemplate, routeDetails.Defaults);
                        }
                        else
                        {
                            routes.MapRoute(routeDetails.Name, routeDetails.RouteTemplate, routeDetails.Defaults);
                        }
                    }
                    routes.MapHttpRoute(text + "sxa", text + "sxa/{controller}/{action}").RouteHandler = new SessionHttpControllerRouteHandler();
                }
            }
        }

        private void ClearRoutes(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Log.Info("HttpRoutesRefresher clearing routes.", this);
            RouteBase[] array = (from route in RouteTable.Routes
                                 where ((Route)route).Url.Contains("sxa/")
                                 select route).ToArray();
            RouteCollection routes = RouteTable.Routes;
            using (routes.GetWriteLock())
            {
                RouteBase[] array2 = array;
                foreach (RouteBase item in array2)
                {
                    routes.Remove(item);
                }
            }

            Log.Info("HttpRoutesRefresher done.", this);
        }
    }

}
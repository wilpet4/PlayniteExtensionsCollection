﻿using Playnite.SDK.Data;
using PlayniteUtilitiesCommon;
using PurchaseDateImporter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FlowHttp;

namespace PurchaseDateImporter.Services
{
    public static class EaLicenseService
    {
        public static Guid PluginId = Guid.Parse("85dd7072-2f20-4e76-a007-41035e390724");
        public const string LibraryName = "Origin";
        public const string LoginUrl = @"https://www.origin.com/";

        public static Dictionary<string, LicenseData> GetLicensesDict()
        {
            var licensesDictionary = new Dictionary<string, LicenseData>();
            var licenses = GetLicenses();
            foreach (var license in licenses)
            {
                licensesDictionary[license.Id] = license;
            }

            return licensesDictionary;
        }

        public static List<LicenseData> GetLicenses()
        {
            var licensesList = new List<LicenseData>();
            var authResponse = GetEaAuthResponse();
            if (authResponse == null)
            {
                return licensesList;
            }

            var entitlementsResponse = GetEaEntitlementsResponse(authResponse);
            if (entitlementsResponse == null)
            {
                return licensesList;
            }

            foreach (var entitlement in entitlementsResponse.Entitlements)
            {
                var offerType = entitlement.OfferType.Replace(" ", "").ToLower();
                if (offerType == "basegame" || offerType == "demo")
                {
                    licensesList.Add(new LicenseData(entitlement.OfferId, entitlement.GrantDate.ToLocalTime(), entitlement.OfferId));
                }
            }

            return licensesList;
        }

        private static EaAuthResponse GetEaAuthResponse()
        {
            using (var webView = Playnite.SDK.API.Instance.WebViews.CreateOffscreenView())
            {
                var authResponseUrl = @"https://accounts.ea.com/connect/auth?client_id=ORIGIN_JS_SDK&response_type=token&redirect_uri=nucleus:rest&prompt=none";
                webView.NavigateAndWait(authResponseUrl);

                var authJson = PlayniteUtilities.GetEmbeddedJsonFromWebViewSource(webView.GetPageSource());
                if (authJson.StartsWith(@"{""error_code"""))
                {
                    // User is not logged in
                    return null;
                }

                if (authJson.IsNullOrEmpty())
                {
                    return null;
                }

                return Serialization.FromJson<EaAuthResponse>(authJson);
            }
        }

        private static EaEntitlementsResponse GetEaEntitlementsResponse(EaAuthResponse authResponse)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"{authResponse.TokenType} {authResponse.AccessToken}",
                ["accept"] = "application/vnd.origin.v3+json; x-cache/force-write",
            };

            var identityResponse = HttpRequestFactory.GetHttpRequest().WithUrl("https://gateway.ea.com/proxy/identity/pids/me").WithHeaders(headers).DownloadString();
            if (!identityResponse.IsSuccess)
            {
                return null;
            }

            var identity = Serialization.FromJson<EaIdentityResponse>(identityResponse.Content);
            // For some reason somtimes the response is in XML format when the Headers contain the
            // Authorization header
            headers.Clear();
            headers["accept"] = "application/vnd.origin.v3+json; x-cache/force-write";
            headers["authtoken"] = authResponse.AccessToken;
            var url = string.Format("https://api1.origin.com/ecommerce2/consolidatedentitlements/{0}?machine_hash=1", identity.Pid.PidId);
            var entitlementsResponseData = HttpRequestFactory.GetHttpRequest()
                .WithUrl(url)
                .WithHeaders(headers)
                .DownloadString();
            return Serialization.FromJson<EaEntitlementsResponse>(entitlementsResponseData.Content);
        }
    }
}
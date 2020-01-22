using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace EtwEvents.WebClient.Pages
{
    public class IndexModel: PageModel
    {
        readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger) {
            _logger = logger;
        }

        public void OnGet() {

        }

        //public string ResourceBase64() {
        //    // Retrieves the requested culture
        //    var rqf = Request.HttpContext.Features.Get<IRequestCultureFeature>();
        //    var culture = rqf.RequestCulture.Culture;
        //    var rs = Resource.ResourceManager.GetResourceSet(culture, true, true);
        //    var resourceBytes = rs != null ? JsonSerializer.SerializeToUtf8Bytes<ResourceSet>(rs) : Array.Empty<byte>();
        //    return Convert.ToBase64String(resourceBytes);
        //}
    }
}

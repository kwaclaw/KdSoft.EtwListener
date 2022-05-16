using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.WebClient.Pages
{
    public class IndexModel: PageModel
    {
        readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger) {
            _logger = logger;
        }

        public void OnGet() {

        }

        public TimeSpan? ClientCertLifeSpan {
            get {
                var expiryDate = HttpContext.Connection.ClientCertificate?.NotAfter;
                if (expiryDate != null) {
                    return expiryDate.Value - DateTime.Now;
                }
                return null;
            }
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

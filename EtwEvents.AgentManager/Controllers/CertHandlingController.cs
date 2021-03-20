using System;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.AgentManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace KdSoft.EtwEvents.AgentManager.Controllers {
    [Authorize]
    [ApiController]
    public class  CertHandlingController: ControllerBase {
        const string RootCertCN = "Elekta-SmartClinic";
        const string ClientCertHeader = "X-ARR-ClientCert";

        static protected readonly X509ChainPolicy _x509Policy;

        //static X509Certificate2 GetRootCertificate() {
        //    using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine)) {
        //        store.Open(OpenFlags.ReadOnly | OpenFlags.MaxAllowed);
        //        var certs = store.Certificates.Find(X509FindType.FindBySubjectName, RootCertCN, false);
        //        foreach (var cert in certs) {
        //            foreach (var extension in cert.Extensions) {
        //                if (extension is X509BasicConstraintsExtension constraintExtension) {
        //                    if (constraintExtension.CertificateAuthority)
        //                        return cert;
        //                }
        //            }
        //        }
        //        return null;
        //    }
        //}

        static CertHandlingController() {
            var policy = new X509ChainPolicy();
            policy.RevocationMode = X509RevocationMode.NoCheck;
            policy.RevocationFlag |= X509RevocationFlag.ExcludeRoot;
            policy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.2"));
            //var rootCertificate = GetRootCertificate();
            //if (rootCertificate != null)
            //    policy.ExtraStore.Add(rootCertificate);
            _x509Policy = policy;
        }

        protected X509Certificate2? GetValidClientCertificate(string certBase64) {
            try {
                var rawCert = Convert.FromBase64String(certBase64);
                var cert = new X509Certificate2(rawCert);

                var chain = new X509Chain();
                chain.ChainPolicy = _x509Policy;
                if (chain.Build(cert))
                    return cert;

                return null;
            }
            catch (FormatException) {
                return null;
            }
            catch (System.Security.Cryptography.CryptographicException) {
                return null;
            }
        }

        protected virtual bool IsAuthorized(string[] authorizedNames, bool isEmail) {
            if (this.User.Identity?.IsAuthenticated ?? false)
                return true;

            if (authorizedNames.Length == 0)
                return false;

            var certHeaderValue = this.HttpContext.Request.Headers[ClientCertHeader];
            if (certHeaderValue == StringValues.Empty)
                return false;
            var cert = GetValidClientCertificate(certHeaderValue);
            if (cert == null)
                return false;

            if (isEmail) {
                var emailName = cert.GetNameInfo(X509NameType.EmailName, false);
                return authorizedNames.Contains(emailName, StringComparer.OrdinalIgnoreCase);
            }
            else {
                var commonName = cert.GetNameInfo(X509NameType.SimpleName, false);
                return authorizedNames.Contains(commonName, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}

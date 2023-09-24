using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;
using KdSoft.Utils;
using WixToolset.Dtf.WindowsInstaller;

namespace EtwEvents.PushAgent.Setup.Tools
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult OpenFileDialog(Session session) {
            try {
                session.Log("Begin OpenFileDialog Action");
                var uiThread = new Thread(() => GetFile(session));
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();
                uiThread.Join();
                session.Log("End OpenFileDialog Action");
            }
            catch (Exception ex) {
                session.Log("Error in OpenFileDialog: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                return ActionResult.Failure;
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ValidateClientCertificate(Session session) {
            try {
                var certPath = (session["CLIENT_CERTIFICATE"] ?? "").Trim();
                if (certPath != string.Empty) {
                    var certPwd = session["CLIENT_CERTIFICATE_PASSWORD"] ?? "";
                    var certToInstall = new X509Certificate2(certPath, certPwd, X509KeyStorageFlags.PersistKeySet);
                    session.Log($"Validated client certificate {certPath}");
                }
                session["CLIENT_CERTIFICATE_VALID"] = "1";
            }
            catch (Exception ex) {
                session["CLIENT_CERTIFICATE_VALID"] = "0";
                session.Log("Error in ValidateClientCertificate: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                ShowMessageDialog(ex.Message, "Client Certificate Not Valid", MessageBoxIcon.Exclamation);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult InstallClientCertificate(Session session) {
            try {
                var certPath = (session["CLIENT_CERTIFICATE"] ?? "").Trim();
                if (certPath == string.Empty)
                    return ActionResult.Success;

                var certPwd = session["CLIENT_CERTIFICATE_PASSWORD"] ?? "";
                var certToInstall = new X509Certificate2(certPath, certPwd, X509KeyStorageFlags.PersistKeySet);
                InstallMachineCertificate(certToInstall);
                session.Log($"Installed client certificate {certPath}");
            }
            catch (Exception ex) {
                session.Log("Error in InstallClientCertificate: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                return ActionResult.Failure;
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult ValidateRootCertificates(Session session) {
            string currentCertPath = "";
            try {
                var certPaths = (session["ROOT_CERTIFICATES"] ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var certPath in certPaths) {
                    var trimmedPath = certPath.Trim();
                    if (trimmedPath == string.Empty)
                        continue;

                    currentCertPath = trimmedPath;
                    var certToInstall = new X509Certificate2(certPath);
                    session.Log($"Validated root certificate {certPath}");
                }
                session["ROOT_CERTIFICATES_VALID"] = "1";
            }
            catch (Exception ex) {
                session["ROOT_CERTIFICATES_VALID"] = "0";
                session.Log("Error in ValidateRootCertificates: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                ShowMessageDialog(currentCertPath + "\n\t" + ex.Message, "Root Certificate Not Valid", MessageBoxIcon.Exclamation);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult InstallRootCertificates(Session session) {
            try {
                var certPaths = (session["ROOT_CERTIFICATES"] ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var certPath in certPaths) {
                    var trimmedPath = certPath.Trim();
                    if (trimmedPath == string.Empty)
                        continue;

                    var certToInstall = new X509Certificate2(certPath);
                    InstallMachineCertificate(certToInstall);
                    session.Log($"Installed root certificate {certPath}");
                }
            }
            catch (Exception ex) {
                session.Log("Error in InstallRootCertificates: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                return ActionResult.Failure;
            }
            return ActionResult.Success;
        }

        static void GetFile(Session session) {
            var multiSelectStr = session["_FileDglMultiSelect"] ?? "";
            if (!bool.TryParse(multiSelectStr, out var multiSelect)) {
                multiSelect = false;
            }
            var fileDialog = new OpenFileDialog {
                Filter = session["_FileDlgFilter"].IfNullOrEmpty("All Files (*.*)|*.*"),
                InitialDirectory = session["_FileDglDir"] ?? "",
                Title = session["_FileDlgTitle"].IfNullOrEmpty("Select File"),
                Multiselect = multiSelect,
            };
            if (fileDialog.ShowDialog() == DialogResult.OK) {
                var targetProperty = session["_FileNamesProperty"].IfNullOrEmpty("FILE_NAMES");
                session[targetProperty] = string.Join(";\n", fileDialog.FileNames);
            }
        }

        /// <summary>
        /// Determines if the certificate is self signed.
        /// </summary>
        /// <param name="certificate">The <see cref="X509Certificate2"/> to check.</param>
        /// <returns><c>true</c> if the certificate is self signed, <c>false</c> otherwise.</returns>
        static bool IsSelfSigned(X509Certificate2 certificate) {
            var subjectRaw = certificate.SubjectName.RawData;
            var issuerRaw = certificate.IssuerName.RawData;
            return subjectRaw.SequenceEqual(issuerRaw);
        }

        /// <summary>
        /// Install the certificate in the LocalMachine scope, selecting the store based on the certificate type.
        /// </summary>
        /// <param name="certificate">The <see cref="X509Certificate2"/> to install.</param>
        static void InstallMachineCertificate(X509Certificate2 certificate) {
            var storeName = StoreName.My;
            var basicConstraintExt = certificate.Extensions["2.5.29.19"] as X509BasicConstraintsExtension;
            if (basicConstraintExt != null) {
                if (basicConstraintExt.CertificateAuthority) {
                    if (IsSelfSigned(certificate))
                        storeName = StoreName.Root;  // root CA
                    else
                        storeName = StoreName.CertificateAuthority;  // intermediate CA
                }
            }
            using var store = new X509Store(storeName, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
        }

        static void ShowMessageDialog(string msg, string caption, MessageBoxIcon icon) {
            var uiThread = new Thread(() => {
                MessageBox.Show(msg, caption, MessageBoxButtons.OK, icon);
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            uiThread.Join();
        }
    }
}

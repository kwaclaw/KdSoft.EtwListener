using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using KdSoft.Utils;
using Newtonsoft.Json.Linq;
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

                    // X509NameType.SimpleName extracts CN from subject (common name)
                    var subjectCN = certToInstall.GetNameInfo(X509NameType.SimpleName, false);
                    session["CLIENT_CERTIFICATE_SUBJECT_CN"] = subjectCN;

                    session.Log($"Validated client certificate {certPath}");
                }
                session["CLIENT_CERTIFICATE_VALID"] = "1";
            }
            catch (Exception ex) {
                session["CLIENT_CERTIFICATE_VALID"] = "0";
                session.Log("Error in ValidateClientCertificate: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
                var errorDlgTitle = session["_validateErrorTitle"] ?? "Client Certificate Not Valid";
                ShowMessageDialog(ex.Message, errorDlgTitle, MessageBoxIcon.Exclamation);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult InstallClientCertificate(Session session) {
            try {
                var data = session.CustomActionData;
                session.Log("InstallClientCertificate - Got CustomActionData: {0}", data.ToString());

                var certPath = (data["CLIENT_CERTIFICATE"] ?? "").Trim();
                if (certPath == string.Empty)
                    return ActionResult.Success;

                var certPwd = data["CLIENT_CERTIFICATE_PASSWORD"] ?? "";
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
                var errorDlgTitle = session["_validateErrorTitle"] ?? "Root Certificate Not Valid";
                ShowMessageDialog(currentCertPath + "\n    " + ex.Message, errorDlgTitle, MessageBoxIcon.Exclamation);
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult InstallRootCertificates(Session session) {
            try {
                var data = session.CustomActionData;
                session.Log("InstallRootCertificates - Got CustomActionData: {0}", data.ToString());

                var certPaths = (data["ROOT_CERTIFICATES"] ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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

        /// <summary>
        /// Populates CustomActionData for deferred custom actions.
        /// </summary>
        [CustomAction]
        public static ActionResult SetDeferredActionData(Session session) {
            var data = new CustomActionData();
            data["ROOT_CERTIFICATES"] = session["ROOT_CERTIFICATES"];
            var dataStr = data.ToString();
            session.Log("InstallRootCertificates - Set CustomActionData for: {0}", dataStr);
            session["InstallRootCertificates"] = dataStr;

            data = new CustomActionData();
            data["CLIENT_CERTIFICATE"] = session["CLIENT_CERTIFICATE"];
            data["CLIENT_CERTIFICATE_PASSWORD"] = session["CLIENT_CERTIFICATE_PASSWORD"];
            dataStr = data.ToString();
            session.Log("InstallClientCertificate - Set CustomActionData: {0}", dataStr);
            session["InstallClientCertificate"] = dataStr;

            data = new CustomActionData();
            data["MANAGER_URL"] = session["MANAGER_URL"];
            data["CLIENT_CERTIFICATE_SUBJECT_CN"] = session["CLIENT_CERTIFICATE_SUBJECT_CN"];
            data["SETTINGS_OVERRIDE"] = session["SETTINGS_OVERRIDE"];
            data["INSTALLFOLDER"] = session["INSTALLFOLDER"];
            data["SETTINGS_OVERRIDE_PATH"] = session["SETTINGS_OVERRIDE_PATH"];

            dataStr = data.ToString();
            session.Log("MergeSettingsOverride - Set CustomActionData: {0}", dataStr);
            session["MergeSettingsOverride"] = dataStr;
            return ActionResult.Success;
        }

        /// <summary>
        /// Merges the JSON file referenced in the "SETTINGS_OVERRIDE" property (the source file) with the currently installed copy of the same file,
        /// and replaces the installed file with the merge result, this simply copies the source file to the install location.
        /// </summary>
        /// <remarks>Due to file copying this action requires elevated privileges: deferred with impersonate="no".</remarks>
        [CustomAction]
        public static ActionResult MergeSettingsOverride(Session session) {
            try {
                var data = session.CustomActionData;
                session.Log("MergeSettingsOverride - Got CustomActionData: {0}", data.ToString());

                var installFolder = data["INSTALLFOLDER"];
                string settingsOverrideDestination;
                // the installed location might be a subfolder of the INSTALLFOLDER
                if (!data.TryGetValue("SETTINGS_OVERRIDE_PATH", out var settingsOverridePath) || string.IsNullOrEmpty(settingsOverridePath)) {
                    settingsOverrideDestination = installFolder;
                }
                else {
                    settingsOverrideDestination = Path.Combine(installFolder, settingsOverridePath);
                }

                JObject newObj;
                if (!data.TryGetValue("SETTINGS_OVERRIDE", out var overrideJson) || !File.Exists(overrideJson)) {
                    newObj = new JObject();
                    overrideJson = null;
                }
                else {
                    newObj = JObject.Parse(File.ReadAllText(overrideJson));
                }

                if (data.TryGetValue("MANAGER_URL", out var managerUrl) && !string.IsNullOrEmpty(managerUrl)) {
                    var controlObj = newObj["Control"];
                    if (controlObj is null) {
                        controlObj = new JObject();
                        newObj["Control"] = controlObj;
                    }
                    controlObj["Uri"] = managerUrl;
                }

                // we set the CLIENT_CERTIFICATE's SubjectCN if one was entered by the user
                if (data.TryGetValue("CLIENT_CERTIFICATE_SUBJECT_CN", out var clientCertSubjectCN) && !string.IsNullOrEmpty(clientCertSubjectCN)) {
                    var controlObj = newObj["Control"];
                    if (controlObj is null) {
                        controlObj = new JObject();
                        newObj["Control"] = controlObj;
                    }

                    var clientCertObj = controlObj["ClientCertificate"];
                    if (clientCertObj is null) {
                        clientCertObj = new JObject();
                        controlObj["ClientCertificate"] = clientCertObj;
                    }

                    clientCertObj["SubjectCN"] = clientCertSubjectCN;
                }

                var fileName = "appsettings.Local.json";
                var installedOverrideJson = Path.Combine(settingsOverrideDestination, fileName);
                if (!File.Exists(installedOverrideJson)) {
                    File.WriteAllText(installedOverrideJson, newObj.ToString(), Encoding.UTF8);
                    session.Log("No settings override file installed: {0}", installedOverrideJson);
                    return ActionResult.Success;
                }

                var installedObj = JObject.Parse(File.ReadAllText(installedOverrideJson));
                var mergeSettings = new JsonMergeSettings {
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Merge,
                    PropertyNameComparison = StringComparison.OrdinalIgnoreCase
                };

                installedObj.Merge(newObj, mergeSettings);
                File.WriteAllText(installedOverrideJson, installedObj.ToString(), Encoding.UTF8);
                session.Log("Settings override file merged: {0}", installedOverrideJson);
            }
            catch (Exception ex) {
                session.Log("Error in MergeSettingsOverride: {0}\r\n StackTrace: {1}", ex.Message, ex.StackTrace);
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
            if (certificate.Extensions["2.5.29.19"] is X509BasicConstraintsExtension basicConstraintExt) {
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

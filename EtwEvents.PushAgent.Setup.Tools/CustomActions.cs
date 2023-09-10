using System;
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

        static void GetFile(Session session) {
            var fileDialog = new OpenFileDialog {
                Filter = session["_FileDlgFilter"].IfNullOrEmpty("All Files (*.*)|*.*"),
                InitialDirectory = session["_FileDglDir"] ?? "",
                Title = session["_FileDlgTitle"].IfNullOrEmpty("Select File"),
                 Multiselect = true,
            };
            if (fileDialog.ShowDialog() == DialogResult.OK) {
                var targetProperty = session["_FileNamesProperty"].IfNullOrEmpty("FILE_NAMES");
                session[targetProperty] = string.Join(";\n", fileDialog.FileNames);
            }
        }
    }
}

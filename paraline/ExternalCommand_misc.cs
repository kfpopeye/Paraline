using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;

namespace paraline
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class aboutbox : IExternalCommand
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(aboutbox));
        public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                pkhCommon.Windows.About_Box ab = new pkhCommon.Windows.About_Box(this.GetType().Assembly);
                System.Windows.Interop.WindowInteropHelper x = new System.Windows.Interop.WindowInteropHelper(ab);
                x.Owner = commandData.Application.MainWindowHandle;
                ab.ShowDialog();
            }
            catch (Exception err)
            {
                _log.Error(err);
                Autodesk.Revit.UI.TaskDialog td = new TaskDialog("Unexpected Error");
                td.MainInstruction = "Paraline has encountered an error and cannot complete.";
                td.MainContent = "Something unexpected has happened and the command cannot complete. More information can be found below.";
                td.ExpandedContent = err.ToString();
                //td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Send bug report.");
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
                TaskDialogResult tdr = td.Show();

                //if (tdr == TaskDialogResult.CommandLink1)
                //{
                //    pkhCommon.Email.SendErrorMessage(commandData.Application.Application.VersionName, commandData.Application.Application.VersionBuild, err, this.GetType().Assembly.GetName());
                //}
            }

            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class kk_help : IExternalCommand
    {
        public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            paraline.macro_help.Launch();
            return Result.Succeeded;
        }
    }

#if DEBUG
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class test_Entitlement : IExternalCommand
    {
        public Autodesk.Revit.UI.Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Properties.Settings.Default.EntCheck = System.DateTime.Today.Subtract(TimeSpan.FromDays(7));
            Properties.Settings.Default.Save();
            if (paraline.UserIsEntitled(commandData))
                TaskDialog.Show("Entitlement Test", "You are entitled.");
            else
                TaskDialog.Show("Entitlement Test", "You are NOT entitled.");
            return Result.Succeeded;
        }
    }
#endif

    public class subcommand_AvailableCheck : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication appdata, CategorySet selectCatagories)
        {
            return true;
        }
    }
}
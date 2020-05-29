using System;
using System.IO;
using System.Windows.Media.Imaging;
using log4net;
using log4net.Config;

using Autodesk;
using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;

namespace paraline
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public partial class paraline : IExternalApplication
    {
        internal static ContextualHelp macro_help = null;
        private static readonly ILog _log = LogManager.GetLogger(typeof(paraline));

        private void CreateRibbonPanel(UIControlledApplication application)
        {
            // This method is used to create the ribbon panel.
            // which contains the controlled application.

            string AddinPath = Properties.Settings.Default.AddinPath;
            string DLLPath = AddinPath + @"\paraline.dll";
            RibbonPanel pkhlPanel = null;

            if (pkhlPanel == null)
                pkhlPanel = application.CreateRibbonPanel("Paraline");

            // Create a Button for macro
            PushButton im_Button = pkhlPanel.AddItem(new PushButtonData("imButton", Constants.O2I_MACRO_NAME, DLLPath, "paraline.ortho2iso_command")) as PushButton;
            im_Button.Image = NewBitmapImage(this.GetType().Assembly, "ortho2iso16x16.png");
            im_Button.LargeImage = NewBitmapImage(this.GetType().Assembly, "ortho2iso.png");
            im_Button.ToolTip = "Runs the Ortho to Iso command";
            im_Button.Visible = true;
            im_Button.AvailabilityClassName = "paraline.ortho2iso_AvailableCheck";
            im_Button.SetContextualHelp(macro_help);

            PushButton eDC_Button = pkhlPanel.AddItem(new PushButtonData("eDCButton", Constants.EDC_MACRO_NAME, DLLPath, "paraline.explodeDC_command")) as PushButton;
            eDC_Button.Image = NewBitmapImage(this.GetType().Assembly, "explodeDC16x16.png");
            eDC_Button.LargeImage = NewBitmapImage(this.GetType().Assembly, "explodeDC.png");
            eDC_Button.ToolTip = "Runs the Explode Detail Component command";
            eDC_Button.Visible = true;
            eDC_Button.AvailabilityClassName = "paraline.explodeDC_AvailableCheck";
            eDC_Button.SetContextualHelp(macro_help);

#if DEBUG
            PushButton test_Button = pkhlPanel.AddItem(new PushButtonData("TestButton", "Test Entitlement", DLLPath, "paraline.test_Entitlement")) as PushButton;
            test_Button.Image = NewBitmapImage(this.GetType().Assembly, "kk_help16x16.png");
            test_Button.LargeImage = NewBitmapImage(this.GetType().Assembly, "kk_help.png");
            test_Button.ToolTip = "Test the entitlement system. Debug mode only.";
            test_Button.Visible = true;
            test_Button.AvailabilityClassName = "paraline.subcommand_AvailableCheck";
#endif

            //Create a slide out
            pkhlPanel.AddSlideOut();

            PushButtonData about_Button = new PushButtonData("aboutButton", "About", DLLPath, "paraline.aboutbox");
            about_Button.Image = NewBitmapImage(this.GetType().Assembly, "about16x16.png");
            about_Button.LargeImage = NewBitmapImage(this.GetType().Assembly, "about.png");
            about_Button.ToolTip = "All about Paraline.";
            about_Button.AvailabilityClassName = "paraline.subcommand_AvailableCheck";

            PushButtonData help_Button = new PushButtonData("helpButton", "Help", DLLPath, "paraline.kk_help");
            help_Button.Image = NewBitmapImage(this.GetType().Assembly, "kk_help16x16.png");
            help_Button.LargeImage = NewBitmapImage(this.GetType().Assembly, "kk_help.png");
            help_Button.ToolTip = "Help about Paraline.";
            help_Button.AvailabilityClassName = "paraline.subcommand_AvailableCheck";

            pkhlPanel.AddStackedItems(about_Button, help_Button);
        }

        /// <summary>
        /// Load a new icon bitmap from embedded resources.
        /// For the BitmapImage, make sure you reference WindowsBase and PresentationCore, and import the System.Windows.Media.Imaging namespace.
        /// Drag images into Resources folder in solution explorer and set build action to "Embedded Resource"
        /// </summary>
        private BitmapImage NewBitmapImage(System.Reflection.Assembly a, string imageName)
        {
            // to read from an external file:
            //return new BitmapImage( new Uri(
            //  Path.Combine( _imageFolder, imageName ) ) );

            Stream s = a.GetManifestResourceStream("paraline.Resources." + imageName);
            BitmapImage img = new BitmapImage();

            img.BeginInit();
            img.StreamSource = s;
            img.EndInit();

            return img;
        }

        internal static bool UserIsEntitled(ExternalCommandData commandData)
        {
            return true;

            //if (Properties.Settings.Default.EntCheck.AddDays(7) > System.DateTime.Today)
            //    return true;

            //string _baseApiUrl = @"https://apps.exchange.autodesk.com/";
            //string _appId = Constants.APP_STORE_ID;

            //UIApplication uiApp = commandData.Application;
            //Autodesk.Revit.ApplicationServices.Application rvtApp = commandData.Application.Application;

            ////Check to see if the user is logged in.
            //if (!Autodesk.Revit.ApplicationServices.Application.IsLoggedIn)
            //{
            //    TaskDialog td = new TaskDialog(Constants.GROUP_NAME);
            //    td.MainInstruction = "Please login to Autodesk 360 first.";
            //    td.MainContent = "This application must check if you are authorized to use it. Login to Autodesk 360 using the same account you used to purchase this app. An internet connection is required.";
            //    td.Show();
            //    return false;
            //}

            ////Get the user id, and check entitlement
            //string userId = rvtApp.LoginUserId;
            //bool isAuthorized = pkhCommon.EntitlementHelper.Entitlement(_appId, userId, _baseApiUrl);

            //if (!isAuthorized)
            //{
            //    TaskDialog td = new TaskDialog(Constants.GROUP_NAME);
            //    td.MainInstruction = "You are not authorized to use this app.";
            //    td.MainContent = "Make sure you login into Autodesk 360 with the same account you used to buy this app. If the app was purchased under a company account, contact your IT department to allow you access.";
            //    td.Show();
            //    return false;
            //}
            //else
            //{
            //    Properties.Settings.Default.EntCheck = System.DateTime.Today;
            //    Properties.Settings.Default.Save();
            //}

            //return isAuthorized;
        }

        #region Event Handlers
        public Autodesk.Revit.UI.Result OnShutdown(UIControlledApplication application)
        { 
            return Autodesk.Revit.UI.Result.Succeeded;
        }

        public Autodesk.Revit.UI.Result OnStartup(UIControlledApplication application)
        {
            CosturaUtility.Initialize();
            string s = this.GetType().Assembly.Location;
            int x = s.IndexOf(@"\paraline.dll", StringComparison.CurrentCultureIgnoreCase);
            s = s.Substring(0, x);
            Properties.Settings.Default.AddinPath = s;
            Properties.Settings.Default.Save();

            XmlConfigurator.Configure(new FileInfo(Properties.Settings.Default.AddinPath + "\\paraline.log4net.config"));
            _log.InfoFormat("Running version: {0}", this.GetType().Assembly.GetName().Version.ToString());
            _log.InfoFormat("Found myself at: {0}", Properties.Settings.Default.AddinPath);

            macro_help = new ContextualHelp(
                ContextualHelpType.ChmFile,
                Path.Combine(
                    Directory.GetParent(Properties.Settings.Default.AddinPath).ToString(), //contents directory
                    "paraline.chm"));

            CreateRibbonPanel(application);

            return Autodesk.Revit.UI.Result.Succeeded;
        }
        #endregion
    }
}
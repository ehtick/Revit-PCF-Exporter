#region Header
#endregion // Header

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.IO;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
//using mySettings = PCF_Functions.Properties.Settings;
using PCF_Taps;

namespace PCF_Exporter
{

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    //[Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        public const string pcfExporterButtonToolTip = "Export piping data to PCF";
        public const string tapConnectionButtonToolTip = "Define a tap connection";

        //Method to get the button image
        BitmapImage NewBitmapImage(Assembly a, string imageName)
        {
            Stream s = a.GetManifestResourceStream(imageName);
            
            BitmapImage img = new BitmapImage();

            img.BeginInit();
            img.StreamSource = s;
            img.EndInit();

            return img;
        }
        
        // get the absolute path of this assembly
        static string ExecutingAssemblyPath = Assembly.GetExecutingAssembly().Location;
        // get ref to assembly
        Assembly exe = Assembly.GetExecutingAssembly();

        public Result OnStartup(UIControlledApplication application)
        {
            AddMenu(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        
        private void AddMenu(UIControlledApplication application)
        {
            //Assembly exe = Assembly.GetExecutingAssembly();

            RibbonPanel rvtRibbonPanel = application.CreateRibbonPanel("PCF Tools");
            PushButtonData data = new PushButtonData("PCFExporter","PCF Exporter",ExecutingAssemblyPath,"PCF_Exporter.FormCaller");
            data.ToolTip = pcfExporterButtonToolTip;
            data.Image = NewBitmapImage(exe, "PCF_Functions.ImgPcfExport16.png");
            data.LargeImage = NewBitmapImage(exe, "PCF_Functions.ImgPcfExport32.png");
            PushButton pushButton = rvtRibbonPanel.AddItem(data) as PushButton;

            data = new PushButtonData("TAPConnection", "Tap Connection", ExecutingAssemblyPath, "PCF_Exporter.TapsCaller");
            data.ToolTip = tapConnectionButtonToolTip;
            data.Image = NewBitmapImage(exe, "PCF_Functions.ImgTapCon16.png");
            data.LargeImage = NewBitmapImage(exe, "PCF_Functions.ImgTapCon32.png");
            pushButton = rvtRibbonPanel.AddItem(data) as PushButton;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class FormCaller : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                PCF_Exporter_form fm = new PCF_Exporter_form(commandData, message);
                fm.ShowDialog();
                PCF_Functions.Properties.Settings.Default.Save();
                fm.Close();
                return Result.Succeeded;
            }

            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }

            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class TapsCaller : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DefineTapConnection dtc = new DefineTapConnection();
            Result result = dtc.defineTapConnection(commandData, ref message, elements);
            if (result == Result.Failed) return Result.Failed;
            else if (result == Result.Succeeded) return Result.Succeeded;
            else return Result.Cancelled;
        }
    }
}
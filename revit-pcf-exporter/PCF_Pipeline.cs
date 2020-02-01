﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Shared.BuildingCoder;
using pdef = PCF_Functions.ParameterDefinition;
using plst = PCF_Functions.ParameterList;
using mySettings = PCF_Functions.Properties.Settings;

namespace PCF_Pipeline
{
    public class PCF_Pipeline_Export
    {
        plst Plst = new plst();

        public StringBuilder Export(string key, Document doc)
        {
            StringBuilder sbPipeline = new StringBuilder();

            try
            {
                //Instantiate collector
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                //Get the elements
                collector.OfClass(typeof (PipingSystemType));
                //Select correct systemType
                PipingSystemType pipingSystemType = (from PipingSystemType st in collector
                    where string.Equals(st.Abbreviation, key)
                    select st).FirstOrDefault();
            
                IEnumerable<pdef> query = from p in Plst.LPAll
                    where string.Equals(p.Domain, "PIPL") &&
                    !string.Equals(p.ExportingTo, "CII") &&
                    !string.Equals(p.ExportingTo, "LDT")
                    select p;

                sbPipeline.Append("PIPELINE-REFERENCE ");
                sbPipeline.Append(key);
                sbPipeline.AppendLine();

                if (PCF_Functions.InputVars.ExportToPlant3DIso)
                {
                    //Facilitate export to Plant 3D iso
                    sbPipeline.Append("    ");
                    sbPipeline.Append("Attribute10");
                    sbPipeline.Append(" ");
                    sbPipeline.Append(key);
                    sbPipeline.AppendLine();

                    string LDTPath = mySettings.Default.LDTPath;
                    if (!string.IsNullOrEmpty(LDTPath) && File.Exists(LDTPath))
                    {
                        var dataSet = Shared.DataHandler.ImportExcelToDataSet(LDTPath, "YES");
                        var data = Shared.DataHandler.ReadDataTable(dataSet.Tables, "Pipelines");
                        var lineId = pipingSystemType.get_Parameter(Plst.PCF_PIPL_LINEID.Guid).AsString();

                        var LdtPars = Plst.LPAll.Where(x => x.ExportingTo == "LDT");
                        foreach (pdef par in LdtPars)
                        {
                            var value = Shared.DataHandler.ReadParameterFromDataTable(lineId, data, par.Name);
                            if (!string.IsNullOrEmpty(value))
                            {
                                sbPipeline.Append("    ");
                                sbPipeline.Append(par.Keyword);
                                sbPipeline.Append(" ");
                                sbPipeline.Append(value);
                                sbPipeline.AppendLine();
                            }
                        }
                    }
                }

                foreach (pdef p in query)
                {
                    if (string.IsNullOrEmpty(pipingSystemType.get_Parameter(p.Guid).AsString())) continue;
                    sbPipeline.Append("    ");
                    sbPipeline.Append(p.Keyword);
                    sbPipeline.Append(" ");
                    sbPipeline.Append(pipingSystemType.get_Parameter(p.Guid).AsString());
                    sbPipeline.AppendLine();
                }
            }
            catch (Exception e)
            {
                BuildingCoderUtilities.ErrorMsg(e.Message);
            }

            return sbPipeline;

            //Debug
            //// Clear the output file
            //System.IO.File.WriteAllBytes(InputVars.OutputDirectoryFilePath + "Pipes.pcf", new byte[0]);

            //// Write to output file
            //using (StreamWriter w = File.AppendText(InputVars.OutputDirectoryFilePath + "Pipes.pcf"))
            //{
            //    w.Write(sbPipes);
            //    w.Close();
            //}
        }
    }
}
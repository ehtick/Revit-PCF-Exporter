﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Text;
using System.Globalization;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Microsoft.Office.Interop.Excel;

using BuildingCoder;
using iv = PCF_Functions.InputVars;

namespace PCF_Functions
{
    public class InputVars
    {
        #region Execution
        //File I/O
        public static string OutputDirectoryFilePath;
        public static string ExcelSheet = "COMP";

        //Execution control
        public static bool ExportAll = true;

        //PCF File Header (preamble) control
        public static string UNITS_BORE = "MM";
        public static bool UNITS_BORE_MM = true;
        public static bool UNITS_BORE_INCH = false;

        public static string UNITS_CO_ORDS = "MM";
        public static bool UNITS_CO_ORDS_MM = true;
        public static bool UNITS_CO_ORDS_INCH = false;

        public static string UNITS_WEIGHT = "KGS";
        public static bool UNITS_WEIGHT_KGS = true;
        public static bool UNITS_WEIGHT_LBS = false;

        public static string UNITS_WEIGHT_LENGTH = "METER";
        public static bool UNITS_WEIGHT_LENGTH_METER = true;
        public static bool UNITS_WEIGHT_LENGTH_INCH = false;
        public static bool UNITS_WEIGHT_LENGTH_FEET = false;
        #endregion Execution

        #region Filters
        //Filters
        public static string SysAbbr = "FVF";
        public static BuiltInParameter SysAbbrParam = BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM;
        public static string PipelineGroupParameterName = "System Abbreviation";
        #endregion Filters

        #region Element parameter definition
        //Shared parameter group
        public const string PCF_GROUP_NAME = "PCF";
        public const BuiltInParameterGroup PCF_BUILTIN_GROUP_NAME = BuiltInParameterGroup.PG_ANALYTICAL_MODEL;

        //PCF specification
        public static string PIPING_SPEC = "STD";
        #endregion
    }

    public class Composer
    {
        #region Preamble
        //PCF Preamble composition
        static StringBuilder sbPreamble = new StringBuilder();
        public static StringBuilder PreambleComposer()
        {
            sbPreamble.Append("ISOGEN-FILES ISOGEN.FLS");
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-BORE "+InputVars.UNITS_BORE);
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-CO-ORDS "+InputVars.UNITS_CO_ORDS);
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-WEIGHT "+InputVars.UNITS_WEIGHT);
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-BOLT-DIA MM");
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-BOLT-LENGTH MM");
            sbPreamble.AppendLine();
            sbPreamble.Append("UNITS-WEIGHT-LENGTH "+InputVars.UNITS_WEIGHT_LENGTH);
            sbPreamble.AppendLine();
            return sbPreamble;
        }
        #endregion

        #region Materials section
        static StringBuilder sbMaterials = new StringBuilder();
        static IEnumerable<IGrouping<string, Element>> materialGroups = null;
        static int groupNumber;
        public static StringBuilder MaterialsSection(IEnumerable<IGrouping<string, Element>> elementGroups)
        {
            materialGroups = elementGroups;
            sbMaterials.Append("MATERIALS");
            foreach (IGrouping<string, Element> group in elementGroups)
            {
                groupNumber++;
                sbMaterials.AppendLine();
                sbMaterials.Append("MATERIAL-IDENTIFIER " + groupNumber);
                sbMaterials.AppendLine();
                sbMaterials.Append("    DESCRIPTION "+group.Key);
            }
            return sbMaterials;
        }
        #endregion
    }

    public class Filter
    {
        BuiltInParameter testParam; ParameterValueProvider pvp; FilterStringRuleEvaluator str;
        FilterStringRule paramFr; public ElementParameterFilter epf;

        public Filter(string valueQualifier, BuiltInParameter parameterName)
        {
            testParam = parameterName;
            pvp = new ParameterValueProvider(new ElementId((int)testParam));
            str = new FilterStringContains();
            paramFr = new FilterStringRule(pvp, str, valueQualifier, false);
            epf = new ElementParameterFilter(paramFr);
        }
    }

    public class Conversion
    {
        const double _inch_to_mm = 25.4;
        const double _foot_to_mm = 12 * _inch_to_mm;
        const double _foot_to_inch = 12;

        /// <summary>
        /// Return a string for a real number formatted to two decimal places.
        /// </summary>
        public static string RealString(double a)
        {
            //return a.ToString("0.##");
            return (Math.Truncate(a * 100) / 100).ToString("0.00", CultureInfo.GetCultureInfo("en-GB"));
        }

        /// <summary>
        /// Return a string for an XYZ point or vector with its coordinates converted from feet to millimetres and formatted to two decimal places.
        /// </summary>
        public static string PointStringMm(XYZ p)
        {
            return string.Format("{0:0.00} {1:0.00} {2:0.00}",
              RealString(p.X * _foot_to_mm),
              RealString(p.Y * _foot_to_mm),
              RealString(p.Z * _foot_to_mm));
        }

        public static string PointStringInch(XYZ p)
        {
            return string.Format("{0:0.00} {1:0.00} {2:0.00}",
              RealString(p.X * _foot_to_inch),
              RealString(p.Y * _foot_to_inch),
              RealString(p.Z * _foot_to_inch));
        }

        public static string PipeSizeToMm(double l)
        {
            return string.Format("{0}", Math.Round(l * 2 * _foot_to_mm));
        }

        public static string PipeSizeToInch(double l)
        {
            return string.Format("{0}", RealString(l*2*_foot_to_inch));
        }

        public static string AngleToPCF(double l)
        {
            return string.Format("{0}", l);
        }

        public static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }
    }

    public class EndWriter
    {
        public static StringBuilder WriteEP1 (Element element, Connector connector)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            XYZ connectorOrigin = connector.Origin;
            double connectorSize = connector.Radius;
            sbEndWriter.Append("    END-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(connectorOrigin));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(connectorOrigin));
            sbEndWriter.Append(" ");
            if (InputVars.UNITS_BORE_MM) sbEndWriter.Append(Conversion.PipeSizeToMm(connectorSize));
            if (InputVars.UNITS_BORE_INCH) sbEndWriter.Append(Conversion.PipeSizeToInch(connectorSize));
            if (string.IsNullOrEmpty(element.LookupParameter(ParameterData.PCF_ELEM_END1).AsString()) == false)
            {
                sbEndWriter.Append(" ");
                sbEndWriter.Append(element.LookupParameter(ParameterData.PCF_ELEM_END1).AsString());
            }
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteEP2(Element element, Connector connector)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            XYZ connectorOrigin = connector.Origin;
            double connectorSize = connector.Radius;
            sbEndWriter.Append("    END-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(connectorOrigin));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(connectorOrigin));
            sbEndWriter.Append(" ");
            if (InputVars.UNITS_BORE_MM) sbEndWriter.Append(Conversion.PipeSizeToMm(connectorSize));
            if (InputVars.UNITS_BORE_INCH) sbEndWriter.Append(Conversion.PipeSizeToInch(connectorSize));
            if (string.IsNullOrEmpty(element.LookupParameter(ParameterData.PCF_ELEM_END2).AsString()) == false)
            {
                sbEndWriter.Append(" ");
                sbEndWriter.Append(element.LookupParameter(ParameterData.PCF_ELEM_END2).AsString());
            }
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteEP2(Element element, XYZ connector, double size)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            XYZ connectorOrigin = connector;
            double connectorSize = size;
            sbEndWriter.Append("    END-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(connectorOrigin));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(connectorOrigin));
            sbEndWriter.Append(" ");
            if (InputVars.UNITS_BORE_MM) sbEndWriter.Append(Conversion.PipeSizeToMm(connectorSize));
            if (InputVars.UNITS_BORE_INCH) sbEndWriter.Append(Conversion.PipeSizeToInch(connectorSize));
            if (string.IsNullOrEmpty(element.LookupParameter(ParameterData.PCF_ELEM_END2).AsString()) == false)
            {
                sbEndWriter.Append(" ");
                sbEndWriter.Append(element.LookupParameter(ParameterData.PCF_ELEM_END2).AsString());
            }
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteBP1(Element element, Connector connector)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            XYZ connectorOrigin = connector.Origin;
            double connectorSize = connector.Radius;
            sbEndWriter.Append("    BRANCH1-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(connectorOrigin));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(connectorOrigin));
            sbEndWriter.Append(" ");
            if (InputVars.UNITS_BORE_MM) sbEndWriter.Append(Conversion.PipeSizeToMm(connectorSize));
            if (InputVars.UNITS_BORE_INCH) sbEndWriter.Append(Conversion.PipeSizeToInch(connectorSize));
            if (string.IsNullOrEmpty(element.LookupParameter(ParameterData.PCF_ELEM_BP1).AsString()) == false)
            {
                sbEndWriter.Append(" ");
                sbEndWriter.Append(element.LookupParameter(ParameterData.PCF_ELEM_BP1).AsString());
            }
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteCP(FamilyInstance familyInstance)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            XYZ elementLocation = ((LocationPoint)familyInstance.Location).Point;
            sbEndWriter.Append("    CENTRE-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(elementLocation));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(elementLocation));
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteCP(XYZ point)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            sbEndWriter.Append("    CENTRE-POINT ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(point));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(point));
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

        public static StringBuilder WriteCO(XYZ point)
        {
            StringBuilder sbEndWriter = new StringBuilder();
            sbEndWriter.Append("    CO-ORDS ");
            if (InputVars.UNITS_CO_ORDS_MM) sbEndWriter.Append(Conversion.PointStringMm(point));
            if (InputVars.UNITS_CO_ORDS_INCH) sbEndWriter.Append(Conversion.PointStringInch(point));
            sbEndWriter.AppendLine();
            return sbEndWriter;
        }

    }

    public class ScheduleCreator
    {
        private UIDocument _uiDoc;
        public Result CreateAllItemsSchedule(UIDocument uiDoc)
        {
            try
            {
                _uiDoc = uiDoc;
                Document doc = _uiDoc.Document;
                FilteredElementCollector sharedParameters = new FilteredElementCollector(doc);
                sharedParameters.OfClass(typeof (SharedParameterElement));

                #region Debug

                ////Debug
                //StringBuilder sbDev = new StringBuilder();
                //var list = new ParameterDefinition().ElementParametersAll;
                //int i = 0;

                //foreach (SharedParameterElement sp in sharedParameters)
                //{
                //    sbDev.Append(sp.GuidValue + "\n");
                //    sbDev.Append(list[i].Guid.ToString() + "\n");
                //    i++;
                //    if (i == list.Count) break;
                //}
                ////sbDev.Append( + "\n");
                //// Clear the output file
                //File.WriteAllBytes(InputVars.OutputDirectoryFilePath + "\\Dev.pcf", new byte[0]);

                //// Write to output file
                //using (StreamWriter w = File.AppendText(InputVars.OutputDirectoryFilePath + "\\Dev.pcf"))
                //{
                //    w.Write(sbDev);
                //    w.Close();
                //}

                #endregion

                Transaction t = new Transaction(doc, "Create items schedules");
                t.Start();

                #region Schedule ALL elements
                ViewSchedule schedAll = ViewSchedule.CreateSchedule(doc, ElementId.InvalidElementId,
                    ElementId.InvalidElementId);
                schedAll.Name = "PCF - ALL Elements";
                schedAll.Definition.IsItemized = false;

                IList<SchedulableField> schFields = schedAll.Definition.GetSchedulableFields();

                foreach (SchedulableField schField in schFields)
                {
                    if (schField.GetName(doc) != "Family and Type") continue;
                    ScheduleField field = schedAll.Definition.AddField(schField);
                    ScheduleSortGroupField sortGroupField = new ScheduleSortGroupField(field.FieldId);
                    schedAll.Definition.AddSortGroupField(sortGroupField);
                }

                foreach (ParameterDefinition pDef in new ParameterDefinition().ElementParametersAll)
                {
                    SharedParameterElement parameter = (from SharedParameterElement param in sharedParameters
                        where param.GuidValue.CompareTo(pDef.Guid) == 0
                        select param).First();
                    SchedulableField queryField =
                        (from fld in schFields
                            where fld.ParameterId.IntegerValue == parameter.Id.IntegerValue
                            select fld).First();

                    ScheduleField field = schedAll.Definition.AddField(queryField);
                    if (pDef.Name != "PCF_ELEM_TYPE") continue;
                    ScheduleFilter filter = new ScheduleFilter(field.FieldId, ScheduleFilterType.HasParameter);
                    schedAll.Definition.AddFilter(filter);
                }
                #endregion

                #region Schedule FILTERED elements
                ViewSchedule schedFilter = ViewSchedule.CreateSchedule(doc, ElementId.InvalidElementId,
                    ElementId.InvalidElementId);
                schedFilter.Name = "PCF - Filtered Elements";
                schedFilter.Definition.IsItemized = false;

                schFields = schedFilter.Definition.GetSchedulableFields();

                foreach (SchedulableField schField in schFields)
                {
                    if (schField.GetName(doc) != "Family and Type") continue;
                    ScheduleField field = schedFilter.Definition.AddField(schField);
                    ScheduleSortGroupField sortGroupField = new ScheduleSortGroupField(field.FieldId);
                    schedFilter.Definition.AddSortGroupField(sortGroupField);
                }

                foreach (ParameterDefinition pDef in new ParameterDefinition().ElementParametersAll)
                {
                    SharedParameterElement parameter = (from SharedParameterElement param in sharedParameters where param.GuidValue.CompareTo(pDef.Guid) == 0
                                                        select param).First();
                    SchedulableField queryField = (from fld in schFields where fld.ParameterId.IntegerValue == parameter.Id.IntegerValue select fld).First();

                    ScheduleField field = schedFilter.Definition.AddField(queryField);
                    if (pDef.Name != "PCF_ELEM_TYPE") continue;
                    ScheduleFilter filter = new ScheduleFilter(field.FieldId, ScheduleFilterType.HasParameter);
                    schedFilter.Definition.AddFilter(filter);
                    filter = new ScheduleFilter(field.FieldId, ScheduleFilterType.NotEqual,"");
                    schedFilter.Definition.AddFilter(filter);
                }
                #endregion

                #region Schedule Pipelines
                ViewSchedule schedPipeline = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_PipingSystem), ElementId.InvalidElementId);
                schedPipeline.Name = "PCF - Pipelines";
                schedPipeline.Definition.IsItemized = false;

                schFields = schedPipeline.Definition.GetSchedulableFields();

                foreach (SchedulableField schField in schFields)
                {
                    if (schField.GetName(doc) != "Family and Type") continue;
                    ScheduleField field = schedPipeline.Definition.AddField(schField);
                    ScheduleSortGroupField sortGroupField = new ScheduleSortGroupField(field.FieldId);
                    schedPipeline.Definition.AddSortGroupField(sortGroupField);
                }

                foreach (ParameterDefinition pDef in new ParameterDefinition().PipelineParametersAll)
                {
                    SharedParameterElement parameter = (from SharedParameterElement param in sharedParameters
                                                        where param.GuidValue.CompareTo(pDef.Guid) == 0
                                                        select param).First();
                    SchedulableField queryField = (from fld in schFields where fld.ParameterId.IntegerValue == parameter.Id.IntegerValue select fld).First();
                    schedPipeline.Definition.AddField(queryField);
                }
                #endregion

                t.Commit();

                sharedParameters.Dispose();

                return Result.Succeeded;
            }
            catch (Exception e)
            {
                Util.InfoMsg(e.Message);
                return Result.Failed;
            }



        }
    }
}
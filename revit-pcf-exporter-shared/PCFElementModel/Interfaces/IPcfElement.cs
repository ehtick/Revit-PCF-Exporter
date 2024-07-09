﻿using Autodesk.Revit.DB;

using PCF_Functions;

using System;
using System.Collections.Generic;
using System.Text;

namespace PCF_Model
{
    internal interface IPcfElement
    {
        Element Element { get; }
        string GetParameterValue(ParameterDefinition pdef);
        object GetParameterValue(string name);
        void SetParameterValue(ParameterDefinition pdef, string value);
        StringBuilder ToPCFString();
    }
}

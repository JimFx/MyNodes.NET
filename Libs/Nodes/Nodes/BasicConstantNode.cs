﻿/*  MyNodes.NET 
    Copyright (C) 2016 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System.Collections.Generic;

namespace MyNodes.Nodes
{
    public class BasicConstantNode : Node
    {
        public BasicConstantNode() : base("Basic", "Constant")
        {
            AddOutput();
            Settings.Add("Value", new NodeSetting(NodeSettingType.Text, "Constant Value", ""));
        }




        public override bool SetSettings(Dictionary<string, string> data)
        {
            Outputs[0].Value = data["Value"];
            return base.SetSettings(data);
        }

        public override string GetNodeDescription()
        {
            return "This node simply stores the specified value. <br/>" +
                   "You can set the value in the settings.";
        }
    }
}
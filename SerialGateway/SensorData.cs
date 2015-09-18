﻿/*  MyNetSensors 
    Copyright (C) 2015 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System;
using System.ComponentModel.DataAnnotations;


namespace MyNetSensors.SerialGateway
{
    public class SensorData
    {
        public SensorDataType? dataType { get; set; }
        public string state { get; set; }

        public SensorData(SensorDataType? dataType, string state)
        {
            this.dataType = dataType;
            this.state = state;
        }

        public override string ToString()
        {
            string s="";

            if (dataType != null)
                s += String.Format("Data type: {0}, ", dataType.ToString());
            else
                s += String.Format("Data type: unknown, ");

            if (state != null)
                s += String.Format("State: {0}\r\n", state);
            else
                s += String.Format("State: unknown\r\n");

            return s;
        }
    }
}
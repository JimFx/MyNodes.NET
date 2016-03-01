﻿/*  MyNodes.NET 
    Copyright (C) 2016 Derwish <derwish.pro@gmail.com>
    License: http://www.gnu.org/licenses/gpl-3.0.txt  
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyNodes.WebController.Code
{
    public class SerialGatewayConfig
    {
        public bool Enable { get; set; }
        public string SerialPortName{ get; set; }
        public int Boudrate{ get; set; }
    }
}

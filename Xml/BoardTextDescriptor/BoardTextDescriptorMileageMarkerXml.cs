﻿using Klyte.DynamicTextProps.Libraries;
using System.Xml;
using System.Xml.Serialization;

namespace Klyte.DynamicTextProps.Overrides
{
    public class BoardTextDescriptorMileageMarkerXml : BoardTextDescriptorParentXml<BoardTextDescriptorMileageMarkerXml>, ILibable
    {
        [XmlAttribute("saveName")]
        public string SaveName { get; set; }
    }

}
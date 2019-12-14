﻿using Klyte.Commons.Interfaces;
using System.Xml;
using System.Xml.Serialization;

namespace Klyte.DynamicTextProps.Overrides
{
    public class BoardTextDescriptorBuildingsXml : BoardTextDescriptorParentXml<BoardTextDescriptorBuildingsXml>, ILibable
    {

        [XmlAttribute("overrideFont")]
        public string m_overrideFont;

        [XmlAttribute("allCaps")]
        public bool m_allCaps = false;
        [XmlAttribute("prefix")]
        public string m_prefix = "";
        [XmlAttribute("suffix")]
        public string m_suffix = "";

        [XmlAttribute("saveName")]
        public string SaveName { get; set; }

        [XmlIgnore]
        public TextType? m_cachedType;
    }
}

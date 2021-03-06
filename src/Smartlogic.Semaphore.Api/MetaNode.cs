﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace Smartlogic.Semaphore.Api
{
    public class MetaNode : ClassificationItem 
    {
        private LinkedList<MetaNode> _childNodes;

        /// <summary>
        ///     Construct the object if an id is provide.
        /// </summary>
        /// <remarks></remarks>
        public MetaNode(string classname, string value, float score, string id, XmlElement element) : base(classname, value, score, id)
        {
            addChildren(element);
        }

        /// <summary>
        ///     Construct the object if no id is provide.
        /// </summary>
        /// <remarks></remarks>
        public MetaNode(string classname, string value, float score, XmlElement element) : base(classname, value, score)
        {
            addChildren(element);
        }

        private void addChildren(XmlElement element)
        {
            _childNodes = new LinkedList<MetaNode>();
            XmlNodeList childNodes = element.ChildNodes;
            if ((childNodes != null) && (childNodes.Count > 0)) {
                foreach (XmlElement childNode in childNodes)
                {
                    if (childNode.Name.Equals("META"))
                    {
                        var value = childNode.GetAttribute("value");
                        string id = childNode.GetAttribute("value");
                        if ((id == null) || (id.Trim().Length == 0)) id = value;

                        var classname = childNode.GetAttribute("name");

                        string scoreString = childNode.GetAttribute("score");
                        float score = 0.0f;
                        if ((scoreString != null) && (scoreString.Trim().Length > 0))
                        {
                            score = float.Parse(scoreString, CultureInfo.InvariantCulture.NumberFormat);
                        }
                        _childNodes.AddLast(new MetaNode(classname, value, score, id, childNode));
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the meta items nesting within this one.
        /// </summary>
        /// <remarks></remarks>
        public IEnumerable<MetaNode> Children
        {
            get { return _childNodes.ToArray(); }
        }


        /// <summary>
        ///     Output the item as an indented string
        /// </summary>
        /// <remarks></remarks>
        override
        public string ToString()
        {
            StringBuilder StringBuilder = new StringBuilder();
            this.Append(StringBuilder, "");
            return StringBuilder.ToString();
        }

        private void Append(StringBuilder StringBuilder, string Indent)
        {
            StringBuilder.Append(Indent).Append("Class Name:").Append(ClassName).Append("  ID:").Append(Id).Append("  Value:").Append(Value).Append("  Score:").Append(Score).Append("\n");
            foreach (MetaNode Child in Children) {
                Child.Append(StringBuilder, Indent + "    ");
            }
        }
    }
}

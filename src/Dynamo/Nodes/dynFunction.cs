﻿//Copyright © Autodesk, Inc. 2012. All rights reserved.
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml;
using Dynamo.Connectors;
using Dynamo.Controls;
using Dynamo.FSchemeInterop.Node;
using Dynamo.PackageManager.UI;
using Dynamo.Utilities;
using System.Windows.Media.Effects;
using Dynamo.Nodes;
using Microsoft.FSharp.Collections;

namespace Dynamo
{
    namespace Nodes
    {
        [NodeDescription("A node with customized internal functionality.")]
        [IsInteractive(false)]
        public class dynFunction : dynNodeWithOneOutput
        {
            protected internal dynFunction(IEnumerable<string> inputs, IEnumerable<string> outputs, FunctionDefinition def)
            {
                _def = def;

                Symbol = def.FunctionId.ToString();

                //Set inputs and output
                SetInputs(inputs);
                foreach (var output in outputs)
                    OutPortData.Add(new PortData(output, "function output", typeof (object)));

                RegisterAllPorts();

                ArgumentLacing = LacingStrategy.Disabled;
            }

            public dynFunction()
            {

            }

            public new string Name 
            {
                get { return this.Definition.Workspace.Name; }
                set
                {
                    this.Definition.Workspace.Name = value;
                    this.RaisePropertyChanged("Name");
                }
            }

            public override string Description
            {
                get { return this.Definition.Workspace.Description; }
                set
                {
                    this.Definition.Workspace.Description = value;
                    this.RaisePropertyChanged("Description");
                }
            }
            public string Symbol { get; protected internal set; }

            public new string Category
            {
                get
                {

                    if (dynSettings.Controller.CustomNodeManager.NodeCategories.ContainsKey(this.Definition.FunctionId))
                        return dynSettings.Controller.CustomNodeManager.NodeCategories[this.Definition.FunctionId];
                    else
                    {
                        return BuiltinNodeCategories.SCRIPTING_CUSTOMNODES;
                    }
                }
            }

            public override void SetupCustomUIElements(dynNodeView nodeUI)
            {
                nodeUI.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(ui_MouseDoubleClick);

                //var editItem = new MenuItem();
                //editItem.Header = "Edit Properties...";
                //editItem.Click += EditCustomNodePropertiesClick;
                //nodeUI.MainContextMenu.Items.Add(editItem);

            }

            void ui_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                Controller.DynamoViewModel.GoToWorkspaceCommand.Execute(_def.FunctionId);
                e.Handled = true;
            }

            FunctionDefinition _def;
            public FunctionDefinition Definition
            {
                get { return _def; }
                internal set
                {
                    _def = value;
                    if (value != null)
                        Symbol = value.FunctionId.ToString();
                }
            }

            public override bool RequiresRecalc
            {
                get
                {
                    //Do we already know we're dirty?
                    bool baseDirty = base.RequiresRecalc;
                    if (baseDirty)
                        return true;

                    return Definition.RequiresRecalc 
                        || Definition.Dependencies.Any(x => x.RequiresRecalc);
                }
                set
                {
                    //Set the base value.
                    base.RequiresRecalc = value;
                    //If we're clean, then notify all internals.
                    if (!value)
                    {
                        if (dynSettings.Controller.Running)
                            dynSettings.FunctionWasEvaluated.Add(Definition);
                        else
                        {
                            //Recursion detection start.
                            Definition.RequiresRecalc = false;

                            foreach (var dep in Definition.Dependencies)
                                dep.RequiresRecalc = false;
                        }
                    }
                }
            }

            protected override InputNode Compile(IEnumerable<string> portNames)
            {
                return SaveResult ? base.Compile(portNames) : new FunctionNode(Symbol, portNames);
            }

            /// <summary>
            /// Sets the inputs of this function.
            /// </summary>
            /// <param name="inputs"></param>
            public void SetInputs(IEnumerable<string> inputs)
            {
                int i = 0;
                foreach (string input in inputs)
                {
                    if (InPortData.Count > i)
                    {
                        InPortData[i].NickName = input;
                    }
                    else
                    {
                        InPortData.Add(new PortData(input, "Input #" + (i + 1), typeof(object)));
                    }

                    i++;
                }

                if (i < InPortData.Count)
                {
                    for (var k = i; k < InPortData.Count; k++)
                        InPorts[k].KillAllConnectors();

                    //MVVM: confirm that extension methods on observable collection do what we expect
                    InPortData.RemoveRange(i, InPortData.Count - i);
                }
            }

            public void SetOutputs(IEnumerable<string> outputs)
            {
                int i = 0;
                foreach (string output in outputs)
                {
                    if (OutPortData.Count > i)
                    {
                        OutPortData[i].NickName = output;
                    }
                    else
                    {
                        OutPortData.Add(new PortData(output, "Output #" + (i + 1), typeof(object)));
                    }

                    i++;
                }

                if (i < OutPortData.Count)
                {
                    for (var k = i; k < OutPortData.Count; k++)
                        OutPorts[k].KillAllConnectors();

                    OutPortData.RemoveRange(i, OutPortData.Count - i);
                }
            }

            protected override void SaveNode(XmlDocument xmlDoc, XmlElement dynEl, SaveContext context)
            {
                //Debug.WriteLine(pd.Object.GetType().ToString());
                XmlElement outEl = xmlDoc.CreateElement("ID");
                
                outEl.SetAttribute("value", Symbol);
                dynEl.AppendChild(outEl);

                outEl = xmlDoc.CreateElement("Name");
                outEl.SetAttribute("value", NickName);
                dynEl.AppendChild(outEl);

                outEl = xmlDoc.CreateElement("Description");
                outEl.SetAttribute("value", Description);
                dynEl.AppendChild(outEl);

                outEl = xmlDoc.CreateElement("Inputs");
                foreach (var input in InPortData.Select(x => x.NickName))
                {
                    var inputEl = xmlDoc.CreateElement("Input");
                    inputEl.SetAttribute("value", input);
                    outEl.AppendChild(inputEl);
                }
                dynEl.AppendChild(outEl);

                outEl = xmlDoc.CreateElement("Outputs");
                foreach (var output in OutPortData.Select(x => x.NickName))
                {
                    var outputEl = xmlDoc.CreateElement("Output");
                    outputEl.SetAttribute("value", output);
                    outEl.AppendChild(outputEl);
                }
                dynEl.AppendChild(outEl);
            }

            protected override void LoadNode(XmlNode elNode)
            {
                foreach (XmlNode subNode in elNode.ChildNodes)
                {
                    if (subNode.Name.Equals("Name"))
                    {
                        NickName = subNode.Attributes[0].Value;
                    }
                }

                foreach (XmlNode subNode in elNode.ChildNodes)
                {
                    if (subNode.Name.Equals("ID"))
                    {
                        Symbol = subNode.Attributes[0].Value;

                        Guid funcId;
                        Guid.TryParse(Symbol, out funcId);

                        // if the dyf does not exist on the search path...
                        if (!dynSettings.Controller.CustomNodeManager.Contains(funcId))
                        {
                            
                            var proxyDef = new FunctionDefinition(funcId)
                            {
                                Workspace =
                                    new FuncWorkspace(
                                        NickName, BuiltinNodeCategories.SCRIPTING_CUSTOMNODES)
                                    {
                                        FilePath = null
                                    }
                            };

                            SetInputs(new List<string>());
                            SetOutputs(new List<string>());
                            RegisterAllPorts();
                            State = ElementState.ERROR;

                            var user_msg = "Failed to load custom node: " + NickName +
                                           ".  Replacing with proxy custom node.";

                            DynamoLogger.Instance.Log(user_msg);

                            // tell custom node loader, but don't provide path, forcing user to resave explicitly
                            dynSettings.Controller.CustomNodeManager.SetFunctionDefinition(funcId, proxyDef);
                            Definition = dynSettings.Controller.CustomNodeManager.GetFunctionDefinition(funcId);
                            ArgumentLacing = LacingStrategy.Disabled;
                            return;
                        }
                    }
                }

                foreach (XmlNode subNode in elNode.ChildNodes)
                {
                    if (subNode.Name.Equals("Outputs"))
                    {
                        int i = 0;
                        foreach (XmlNode outputNode in subNode.ChildNodes)
                        {
                            var data = new PortData(outputNode.Attributes[0].Value, "Output #" + (i + 1), typeof(object));

                            if (OutPortData.Count > i)
                            {
                                OutPortData[i] = data;
                            }
                            else
                            {
                                OutPortData.Add(data);
                            }

                            i++;
                        }
                    }
                    else if (subNode.Name.Equals("Inputs"))
                    {
                        int i = 0;
                        foreach (XmlNode inputNode in subNode.ChildNodes)
                        {
                            var data = new PortData(inputNode.Attributes[0].Value, "Input #" + (i + 1), typeof(object));

                            if (InPortData.Count > i)
                            {
                                InPortData[i] = data;
                            }
                            else
                            {
                                InPortData.Add(data);
                            }

                            i++;
                        }
                    }
                    #region Legacy output support
                    else if (subNode.Name.Equals("Output"))
                    {
                        var data = new PortData(subNode.Attributes[0].Value, "function output", typeof(object));

                        if (OutPortData.Any())
                            OutPortData[0] = data;
                        else
                            OutPortData.Add(data);
                    }
                    #endregion
                }

                RegisterAllPorts();

                //argument lacing on functions should be set to disabled
                //by default in the constructor, but for any workflow saved
                //before this was the case, we need to ensure it here.
                ArgumentLacing = LacingStrategy.Disabled;

                // we've found a custom node, we need to attempt to load its guid.  
                // if it doesn't exist (i.e. its a legacy node), we need to assign it one,
                // deterministically
                Guid funId;
                try
                {
                    funId = Guid.Parse(Symbol);
                }
                catch
                {
                    funId = GuidUtility.Create(GuidUtility.UrlNamespace, elNode.Attributes["nickname"].Value);
                    Symbol = funId.ToString();
                }

                Definition = dynSettings.Controller.CustomNodeManager.GetFunctionDefinition(funId);
            }

            public override void Evaluate(FSharpList<FScheme.Value> args, Dictionary<PortData, FScheme.Value> outPuts)
            {
                if (OutPortData.Count > 1)
                {
                    var query = (Evaluate(args) as FScheme.Value.List).Item.Zip(
                        OutPortData, (value, data) => new { value, data });

                    foreach (var result in query)
                        outPuts[result.data] = result.value;
                }
                else
                    base.Evaluate(args, outPuts);
            }

            public override FScheme.Value Evaluate(FSharpList<FScheme.Value> args)
            {
                return ((FScheme.Value.Function)Controller.FSchemeEnvironment.LookupSymbol(Symbol))
                    .Item.Invoke(args);
            }
        }

        [NodeName("Output")]
        [NodeCategory(BuiltinNodeCategories.CORE_PRIMITIVES)]
        [NodeDescription("A function output")]
        [IsInteractive(false)]
        public class dynOutput : dynNodeModel
        {
            TextBox tb;
            private string symbol = "";

            public dynOutput()
            {
                InPortData.Add(new PortData("", "", typeof(object)));

                RegisterAllPorts();
            }

            public override void SetupCustomUIElements(dynNodeView nodeUI)
            {
                //add a text box to the input grid of the control
                tb = new TextBox();
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Center;
                nodeUI.inputGrid.Children.Add(tb);
                Grid.SetColumn(tb, 0);
                Grid.SetRow(tb, 0);

                //turn off the border
                var backgroundBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                tb.Background = backgroundBrush;
                tb.BorderThickness = new Thickness(0);

                tb.DataContext = this;
                var bindingSymbol = new Binding("Symbol")
                {
                    Mode = BindingMode.TwoWay,
                    Converter = new StringDisplay()
                };
                tb.SetBinding(TextBox.TextProperty, bindingSymbol);

                tb.TextChanged += tb_TextChanged;
            }

            void tb_TextChanged(object sender, TextChangedEventArgs e)
            {
                Symbol = tb.Text;
            }

            public override bool RequiresRecalc
            {
                get
                {
                    return false;
                }
                set { }
            }

            public string Symbol
            {
                get
                {
                    return symbol;
                }
                set
                {
                    symbol = value;
                    RaisePropertyChanged("Symbol");
                }
            }

            protected override void SaveNode(XmlDocument xmlDoc, XmlElement dynEl, SaveContext context)
            {
                //Debug.WriteLine(pd.Object.GetType().ToString());
                XmlElement outEl = xmlDoc.CreateElement("Symbol");
                outEl.SetAttribute("value", Symbol);
                dynEl.AppendChild(outEl);
            }

            protected override void LoadNode(XmlNode elNode)
            {
                foreach (XmlNode subNode in elNode.ChildNodes)
                {
                    if (subNode.Name == "Symbol")
                    {
                        Symbol = subNode.Attributes[0].Value;
                    }
                }
            }
        }

        [NodeName("Input")]
        [NodeCategory(BuiltinNodeCategories.CORE_PRIMITIVES)]
        [NodeDescription("A function parameter")]
        [NodeSearchTags("variable", "argument", "parameter")]
        [IsInteractive(false)]
        public class dynSymbol : dynNodeModel
        {
            TextBox tb;
            private string symbol = "";

            public dynSymbol()
            {
                OutPortData.Add(new PortData("", "Symbol", typeof(object)));

                RegisterAllPorts();
            }

            public override void SetupCustomUIElements(Controls.dynNodeView nodeUI)
            {
                //add a text box to the input grid of the control
                tb = new TextBox();
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Center;
                nodeUI.inputGrid.Children.Add(tb);
                Grid.SetColumn(tb, 0);
                Grid.SetRow(tb, 0);

                //turn off the border
                var backgroundBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                tb.Background = backgroundBrush;
                tb.BorderThickness = new Thickness(0);

                tb.DataContext = this;
                var bindingSymbol = new Binding("Symbol")
                {
                    Mode = BindingMode.TwoWay
                };
                tb.SetBinding(TextBox.TextProperty, bindingSymbol);

                tb.TextChanged += tb_TextChanged;
            }

            void tb_TextChanged(object sender, TextChangedEventArgs e)
            {
                Symbol = tb.Text;
            }

            public override bool RequiresRecalc
            {
                get
                {
                    return false;
                }
                set { }
            }

            //MVVM: removed direct set of tb.text
            public string Symbol
            {
                get
                {
                    //return tb.Text;
                    return symbol;
                }
                set
                {
                    //tb.Text = value;
                    symbol = value;
                    RaisePropertyChanged("Symbol");
                }
            }

            protected internal override INode Build(Dictionary<dynNodeModel, Dictionary<int, INode>> preBuilt, int outPort)
            {
                Dictionary<int, INode> result;
                if (!preBuilt.TryGetValue(this, out result))
                {
                    result = new Dictionary<int, INode>();
                    result[outPort] = new SymbolNode(GUID.ToString());
                    preBuilt[this] = result;
                }
                return result[outPort];
            }

            protected override void SaveNode(XmlDocument xmlDoc, XmlElement dynEl, SaveContext context)
            {
                //Debug.WriteLine(pd.Object.GetType().ToString());
                XmlElement outEl = xmlDoc.CreateElement("Symbol");
                outEl.SetAttribute("value", Symbol);
                dynEl.AppendChild(outEl);
            }

            protected override void LoadNode(XmlNode elNode)
            {
                foreach (XmlNode subNode in elNode.ChildNodes)
                {
                    if (subNode.Name == "Symbol")
                    {
                        Symbol = subNode.Attributes[0].Value;
                    }
                }
            }
        }

        #region Disabled Anonymous Function Node
        //[RequiresTransaction(false)]
        //[IsInteractive(false)]
        //public class dynAnonFunction : dynElement
        //{
        //   private INode entryPoint;

        //   public dynAnonFunction(IEnumerable<string> inputs, string output, INode entryPoint)
        //   {
        //      int i = 1;
        //      foreach (string input in inputs)
        //      {
        //         InPortData.Add(new PortData(null, input, "Input #" + i++, typeof(object)));
        //      }

        //      OutPortData = new PortData(null, output, "function output", typeof(object));

        //      entryPoint = entryPoint;

        //      NodeUI.RegisterInputsAndOutput();
        //   }

        //   protected internal override ProcedureCallNode Compile(IEnumerable<string> portNames)
        //   {
        //      return new AnonymousFunctionNode(portNames, entryPoint);
        //   }
        //}
        #endregion
    }

    public class FunctionDefinition
    {
        internal FunctionDefinition() : this(Guid.NewGuid()) { }

        internal FunctionDefinition(Guid id)
        {
            FunctionId = id;
            RequiresRecalc = true;
        }

        public Guid FunctionId { get; private set; }
        public dynWorkspaceModel Workspace { get; internal set; }
        public List<Tuple<int, dynNodeModel>> OutPortMappings { get; internal set; }
        public List<Tuple<int, dynNodeModel>> InPortMappings { get; internal set; }
        public bool RequiresRecalc { get; internal set; }

        /// <summary>
        /// A list of all dependencies with no duplicates
        /// </summary>
        public IEnumerable<FunctionDefinition> Dependencies
        {
            get
            {
                return findAllDependencies(new HashSet<FunctionDefinition>());
            }
        }

        /// <summary>
        /// A list of all direct dependencies without duplicates
        /// </summary>
        public IEnumerable<FunctionDefinition> DirectDependencies
        {
            get
            {
                return findDirectDependencies();
            }
        }

        private IEnumerable<FunctionDefinition> findAllDependencies(HashSet<FunctionDefinition> dependencySet)
        {
            var query = this.DirectDependencies.Where(def => !dependencySet.Contains(def));

            foreach (var definition in query)
            {
                yield return definition;
                dependencySet.Add(definition);
                foreach (var def in definition.findAllDependencies(dependencySet))
                    yield return def;
            }
        }

        private IEnumerable<FunctionDefinition> findDirectDependencies()
        {
            var query = Workspace.Nodes
                                 .Where(node => node is dynFunction)
                                 .Select(node => (node as dynFunction).Definition)
                                 .Where((def) => def != this)
                                 .Distinct();

            foreach (var definition in query)
            {
                yield return definition;
            }
        }

    }
}

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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dynamo.Commands;
using Dynamo.Controls;
using Dynamo.Nodes;

namespace Dynamo.Nodes
{
    public class dynSearchController
    {

        #region Properties

        private static SearchDictionary<dynNodeUI> _searchDict = new SearchDictionary<dynNodeUI>();

        private static List<dynNode> localNodes = new List<dynNode>();

        private static ObservableCollection< dynNodeUI > visibleNodes = new ObservableCollection<dynNodeUI>();
        public ObservableCollection<dynNodeUI> VisibleNodes { get { return visibleNodes; } }

        public int NumSearchResults { get; set; }

        private dynSearchUI _view;
        public dynSearchUI View { get { return _view;  } }

        private dynBench _bench;
        public dynBench Bench { get { return _bench; } }

        #endregion


        public dynSearchController( dynBench bench )
        {
            this.NumSearchResults = 10;
            this._bench = bench;
            this._view = new dynSearchUI(this);
        }

        internal void SearchAndUpdateUI(string search)
        {
            visibleNodes.Clear();

            //if (search == "") return;

            foreach (var node in this.Search(search))
            {
                visibleNodes.Add(node);
            }

            this.View.SetSelected(0);
        }

        internal List<dynNodeUI> Search(string search)
        {
            return _searchDict.FuzzySearch(search, this.NumSearchResults);
        }

        public void KeyHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.SendSelectedToWorkspace();
            }
            else if (e.Key == Key.Down)
            {
                this.View.SelectNext();
            }
            else if (e.Key == Key.Up)
            {
                this.View.SelectPrevious();
            }
        }

        public void SendSelectedToWorkspace()
        {
            View.Visibility = Visibility.Collapsed;

            if (visibleNodes.Count == 0) return;

            var selectedIndex = View.SelectedIndex();

            // none of the elems are selected, return 
            if (selectedIndex == -1)
                return;

            DynamoCommands.CreateNodeCmd.Execute(new Dictionary<string, object>()
                {
                    {"name", visibleNodes[selectedIndex].NickName},
                    {"transformFromOuterCanvasCoordinates", true}
                });

        }

        public void Add(Type type, string name)
        {

            dynNode newNode = null;

            try
            {
                var obj = Activator.CreateInstance(type);
                newNode = (dynNode)obj;
            }
            catch (Exception e)
            {
                Bench.Log("Error creating search element for: " + name);
                Bench.Log(e.InnerException);
                return;
            }

            var nodeUI = newNode.NodeUI;
            nodeUI.DisableInteraction();
            nodeUI.Margin = new Thickness(5, 30, 5, 5);
            nodeUI.LayoutTransform = new ScaleTransform(0.8, 0.8);

            nodeUI.MouseDown += delegate
            {
                Bench.BeginDragElement(nodeUI, name, Mouse.GetPosition(nodeUI));
                nodeUI.Visibility = System.Windows.Visibility.Hidden;
            };

            nodeUI.GUID = new Guid();

            _searchDict.Add(nodeUI, name.Split(' ').Where(x => x.Length > 0));
            _searchDict.Add(nodeUI, name);
            _searchDict.AddName(nodeUI, name);

            localNodes.Add(newNode);

        }

    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Microsoft.Win32;
using Newtonsoft.Json;
using Ocelot.Configuration.File;

namespace OcelotRoutesViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly IReadOnlyList<Color> Palette = new[]
        {
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.Yellow,
            Color.Magenta,
            Color.Aqua,
            Color.Beige,
            Color.Brown
        };
        
        private readonly Dictionary<string, Color> _downstreamServiceColors = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Graph _graph = new Graph();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var graphViewer = new GraphViewer();

            graphViewer.BindToPanel(mainPanel);

            var dialog = new OpenFileDialog
            {
                CheckPathExists = true,
                CheckFileExists = true,
                DefaultExt = ".json",
            };

            if (dialog.ShowDialog(this) != true)
                return;

            var jsonFile = dialog.FileName;

            var ocelotConfiguration = JsonConvert.DeserializeObject<FileConfiguration>(File.ReadAllText(jsonFile));

            if(ocelotConfiguration is null) return;

            var order = 0;
            foreach (var route in ocelotConfiguration.Routes)
            {
                var downstreamHost = route.DownstreamHostAndPorts.First().Host.ToLowerInvariant();

                if (!_downstreamServiceColors.ContainsKey(downstreamHost))
                {
                    _downstreamServiceColors[downstreamHost] = Palette[_downstreamServiceColors.Count % Palette.Count];
                }

                var upstreamPathTemplate = route.UpstreamPathTemplate.ToLowerInvariant();

                var upstreamPathTemplateParts = upstreamPathTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);

                Node? previousNode = null;
                var nodeId = string.Empty;

                foreach (var upstreamPathTemplatePart in upstreamPathTemplateParts)
                {
                    var nodeLabel = $"/{upstreamPathTemplatePart}";

                    nodeId += nodeLabel;

                    var node = _graph.FindNode(nodeId);

                    if (node == null)
                    {
                        node = _graph.AddNode(nodeId);
                        node.LabelText = nodeLabel;
                    }

                    if (previousNode != null && previousNode.OutEdges.All(edge => edge.Target != nodeId))
                    {
                        _graph.AddEdge(previousNode.Id, nodeId);
                    }

                    previousNode = node;
                }

                if(previousNode != null)
                {
                    if (previousNode.UserData == null)
                    {
                        previousNode.UserData = new NodeData
                        {
                            LastSegment = previousNode.LabelText,
                            IsEverything = previousNode.Id.EndsWith("/{everything}")
                        };
                    }

                    ((NodeData)previousNode.UserData).AddRoute(route, order++);

                    previousNode.LabelText = ((NodeData)previousNode.UserData).GetLabel();

                    previousNode.Attr.FillColor = _downstreamServiceColors[downstreamHost];
                }
            }

            foreach (var downstreamHost in _downstreamServiceColors.Keys)
            {
                downstreamHostsList.Items.Add(downstreamHost);
            }

            downstreamHostsList.SelectAll();

            downstreamHostsList.SelectionChanged += DownstreamHostsListOnSelectionChanged;

            graphViewer.Graph = _graph;
        }

        private void DownstreamHostsListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedHosts = downstreamHostsList.SelectedItems.Cast<string>();

            var selectedHostsColors = selectedHosts.ToDictionary(h => h, h => _downstreamServiceColors[h]);

            foreach (var node in _graph.Nodes)
            {
                var nodeData = node.UserData as NodeData;
                if(nodeData == null) continue;

                if (selectedHostsColors.ContainsKey(nodeData.Downstream.First().DownstreamHost))
                {
                    node.Attr.FillColor = selectedHostsColors[nodeData.Downstream.First().DownstreamHost];
                }
                else
                {
                    node.Attr.FillColor = Color.Transparent;
                }
            }
        }

        private void OnCheckCorrectness(object sender, RoutedEventArgs e)
        {
            foreach (var node in _graph.Nodes.Where(n => n.UserData is NodeData))
            {
                var currentNode = node;

                while (true)
                {
                    if(!currentNode.InEdges.Any())
                        break;

                    var inEdge = currentNode.InEdges.Single();

                    var parentNode = inEdge.SourceNode;

                    if (CollideWithEverything(parentNode, node))
                    {
                        node.Attr.LineWidth = 3;

                        break;
                    }

                    currentNode = parentNode;
                }

            }
        }

        private bool CollideWithEverything(Node node, Node nodeToTest)
        {
            var nodeData = (NodeData)nodeToTest.UserData;

            return node.OutEdges.Select(n => n.TargetNode)
                .Where(n => !ReferenceEquals(n, nodeToTest) && n.UserData is NodeData)
                .Select(n => (NodeData)n.UserData)
                .Any(d => d.CollidesWith(nodeData)
                );
        }
    }
}

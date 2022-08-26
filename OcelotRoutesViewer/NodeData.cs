using System;
using System.Collections.Generic;
using System.Linq;
using Ocelot.Configuration.File;

namespace OcelotRoutesViewer;

public sealed class NodeData
{
    public string LastSegment { get; set; }
    public bool IsEverything { get; set; }
    public List<DownstreamService> Downstream { get; } = new List<DownstreamService>();

    public void AddRoute(FileRoute route, int order)
    {
        var downstreamService = new DownstreamService(route.DownstreamHostAndPorts.First().Host)
        {
            Priority = route.Priority,
            Order = order
        };

        Downstream.Add(downstreamService);

        foreach (var method in route.UpstreamHttpMethod)
        {
            downstreamService.HttpMethods.Add(method);
        }
    }

    public HashSet<string> GetMethods()
    {
        var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var downstreamService in Downstream)
        {
            result.UnionWith(downstreamService.HttpMethods);
        }

        return result;
    }

    public string GetLabel()
    {
        var label = $"{LastSegment}\n{string.Join(',', GetMethods().OrderBy(m => m).Select(m => m.ToUpper()))}";
        if (Downstream.Select(d => d.DownstreamHost).Distinct().Count() > 1)
            label += "\n<MultiService>";

        return label;
    }

    public bool CollidesWith(NodeData nodeData)
    {
        if (!IsEverything) return false;

        foreach (var downstreamService in Downstream)
        {
            var serviceWithSameMethods = nodeData.Downstream
                .FirstOrDefault(d => d.HttpMethods.Overlaps(downstreamService.HttpMethods));
            if(serviceWithSameMethods == null) continue;

            return serviceWithSameMethods.Order > downstreamService.Order;
        }

        return false;
    }
}

public class DownstreamService
{
    public DownstreamService(string downstreamHost)
    {
        DownstreamHost = downstreamHost.ToLowerInvariant();
    }

    public int Order { get; set; }
    public int Priority { get; set; }
    public string DownstreamHost { get; }
    public HashSet<string> HttpMethods { get; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
}

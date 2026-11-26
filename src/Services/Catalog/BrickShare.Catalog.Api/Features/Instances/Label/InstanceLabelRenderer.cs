using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text;

using QRCoder;

namespace BrickShare.Catalog.Api.Features.Instances.Label;

public static class InstanceLabelRenderer
{
    private const int LabelWidth = 50;
    private const int LabelHeight = 30;
    private const int SheetWidth = 210;
    private const int SheetColumns = 4;
    private const int SheetMargin = 5;
    private const int MaxNameLength = 14;

    public static string RenderLabel(InstanceView view) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" width="{LabelWidth}mm" height="{LabelHeight}mm" viewBox="0 0 {LabelWidth} {LabelHeight}">{LabelBody(view)}</svg>""";

    public static string RenderSheet(IReadOnlyList<InstanceView> views)
    {
        var rows = (views.Count + SheetColumns - 1) / SheetColumns;
        var height = rows * LabelHeight + 2 * SheetMargin;
        var body = new StringBuilder();

        for (var index = 0; index < views.Count; index++)
        {
            var x = SheetMargin + (index % SheetColumns) * LabelWidth;
            var y = SheetMargin + (index / SheetColumns) * LabelHeight;
            body.Append(CultureInfo.InvariantCulture, $"""<g transform="translate({x} {y})">{LabelBody(views[index])}</g>""");
        }

        return $"""<svg xmlns="http://www.w3.org/2000/svg" width="{SheetWidth}mm" height="{height}mm" viewBox="0 0 {SheetWidth} {height}">{body}</svg>""";
    }

    private static string LabelBody(InstanceView view)
    {
        var code = view.InventoryCode.ToString();

        return $"""
            {QrImage(code)}
            <text x="12" y="27" font-family="monospace" font-size="4" font-weight="bold" text-anchor="middle">{code}</text>
            <text x="26" y="10" font-family="sans-serif" font-size="3">{SecurityElement.Escape(Shorten(view.SetName))}</text>
            <text x="26" y="16" font-family="sans-serif" font-size="3">{view.LegoSetNumber.Value}</text>
            """;
    }

    private static string QrImage(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.H);
        using var qrCode = new SvgQRCode(data);

        var svg = qrCode.GetGraphic(1, "#000000", "#FFFFFF", true, SvgQRCode.SizingMode.ViewBoxAttribute);
        var href = $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";

        return $"""<image x="2" y="2" width="20" height="20" href="{href}"/>""";
    }

    private static string Shorten(string name) =>
        name.Length <= MaxNameLength ? name : $"{name[..(MaxNameLength - 1)]}…";
}
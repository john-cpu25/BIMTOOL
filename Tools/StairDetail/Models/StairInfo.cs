
using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace RincoNhan.Tools.StairDetail;

internal class StairInfo : ICloneable
{
    // Stair faces — geometry references
    public PlanarFace StartFace { get; set; }
    public PlanarFace EndFace { get; set; }

    // Upper landing faces
    public PlanarFace UpVertical { get; set; }
    public PlanarFace UpTop { get; set; }
    public PlanarFace UpBot { get; set; }

    // Riser face (between deck and upper landing)
    public PlanarFace Riser { get; set; }

    // Deck (inclined slab) faces
    public PlanarFace DeckTop { get; set; }
    public PlanarFace DeckBot { get; set; }

    // Lower landing faces
    public PlanarFace DownVertical { get; set; }
    public PlanarFace DownTop { get; set; }
    public PlanarFace DownBot { get; set; }

    // Wall faces (Type 3 and 4)
    public PlanarFace WallTop { get; set; }
    public PlanarFace WallBot { get; set; }

    // Beam faces — lower beam
    public PlanarFace BeamDown_Top { get; set; }
    public PlanarFace BeamDown_Bot { get; set; }
    public PlanarFace BeamDown_Left { get; set; }
    public PlanarFace BeamDown_Right { get; set; }

    // Beam faces — upper beam
    public PlanarFace BeamUp_Top { get; set; }
    public PlanarFace BeamUp_Bot { get; set; }
    public PlanarFace BeamUp_Left { get; set; }
    public PlanarFace BeamUp_Right { get; set; }

    // Deck edges for dimensioning
    public Edge DeckTop_EdgeTop { get; set; }
    public Edge DeckTop_EdgeBot { get; set; }
    public Edge DeckBot_EdgeTop { get; set; }
    public Edge DeckBot_EdgeBot { get; set; }

    // Stair type: 1, 2, 3, or 4 (-1 = not identified)
    public int Type { get; set; } = -1;

    // Direction: 1 = left-to-right, -1 = right-to-left
    public int IsLeftToRight { get; set; } = 1;

    // Geometry parameters
    public double Thickness { get; set; }
    public double Cover { get; set; }
    public double DoanNeo { get; set; }
    public double ChieuDaiToiThieu { get; set; }

    // Planes
    public Plane Mp_start { get; set; }
    public Plane Mp_view { get; set; }

    // Rebar parameters
    public double Rebar_range { get; set; }
    public XYZ Rebar_Direction { get; set; }
    public RebarBarType ThepChu { get; set; }
    public double ThepChu_KhoangRai { get; set; }
    public RebarBarType ThepGiaCuong { get; set; }
    public double ThepGiaCuong_KhoangRai { get; set; }

    // Tag symbols
    public ElementId Symbol_Main { get; set; }
    public ElementId Symbol_GC { get; set; }

    // Host element
    public Element Host { get; set; }
    public string Partition { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }

    /// <summary>
    /// Debug method — places TextNotes at each face center in the active view.
    /// </summary>
    public void CheckInfo(Document doc)
    {
        PlaceDebugNote(doc, UpTop, "UpTop");
        PlaceDebugNote(doc, UpBot, "UpBot");
        PlaceDebugNote(doc, UpVertical, "UpVertical");
        PlaceDebugNote(doc, Riser, "Riser");
        PlaceDebugNote(doc, DeckTop, "DeckTop");
        PlaceDebugNote(doc, DeckBot, "DeckBot");
        PlaceDebugNote(doc, DownTop, "DownTop");
        PlaceDebugNote(doc, DownBot, "DownBot");
        PlaceDebugNote(doc, DownVertical, "DownVertical");
        PlaceDebugNote(doc, WallTop, "WallTop");
        PlaceDebugNote(doc, WallBot, "WallBot");
        PlaceDebugEdgeNote(doc, DeckTop_EdgeTop, "DeckTop_EdgeTop");
        PlaceDebugEdgeNote(doc, DeckTop_EdgeBot, "DeckTop_EdgeBot");
        PlaceDebugEdgeNote(doc, DeckBot_EdgeTop, "DeckBot_EdgeTop");
        PlaceDebugEdgeNote(doc, DeckBot_EdgeBot, "DeckBot_EdgeBot");
    }

    private void PlaceDebugNote(Document doc, PlanarFace face, string label)
    {
        if ((GeometryObject)(object)face == (GeometryObject)null) return;
        BoundingBoxUV bb = ((Face)face).GetBoundingBox();
        XYZ center = ((Face)face).Evaluate((bb.Max + bb.Min) / 2.0);
        TextNote note = TextNote.Create(doc, ((Element)doc.ActiveView).Id, center, label, doc.GetDefaultElementTypeId((ElementTypeGroup)12));
        note.AddLeader((TextNoteLeaderTypes)0);
        note.GetLeaders().First().End = center;
    }

    private void PlaceDebugEdgeNote(Document doc, Edge edge, string label)
    {
        if ((GeometryObject)(object)edge == (GeometryObject)null) return;
        Curve c = edge.AsCurve();
        TextNote note = TextNote.Create(doc, ((Element)doc.ActiveView).Id, c.GetEndPoint(0), label, doc.GetDefaultElementTypeId((ElementTypeGroup)12));
        note.AddLeader((TextNoteLeaderTypes)0);
        note.GetLeaders().First().End = GeomUtil.Middle2Point(c.GetEndPoint(0), c.GetEndPoint(1));
    }

    /// <summary>
    /// Refreshes all PlanarFace and Edge references from the host element.
    /// </summary>
    public void Regenerate()
    {
        PropertyInfo[] properties = GetType().GetProperties();
        foreach (PropertyInfo propertyInfo in properties)
        {
            object value = propertyInfo.GetValue(this);
            PlanarFace pf = (PlanarFace)((value is PlanarFace) ? value : null);
            if ((GeometryObject)(object)pf != (GeometryObject)null)
            {
                GeometryObject geom = Host.GetGeometryObjectFromReference(((Face)pf).Reference);
                PlanarFace refreshed = (PlanarFace)(object)((geom is PlanarFace) ? geom : null);
                if ((GeometryObject)(object)refreshed != (GeometryObject)null)
                    propertyInfo.SetValue(this, refreshed);
            }

            object value2 = propertyInfo.GetValue(this);
            Edge edge = (Edge)((value2 is Edge) ? value2 : null);
            if (edge != null)
                propertyInfo.SetValue(this, Host.GetGeometryObjectFromReference(edge.Reference));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Rhino.FileIO;

namespace RincoModeling.Tools.ImportRhino
{
    public class ImportRhinoLogic
    {
        public int Import3dmAsDirectShape(Document doc, string filePath)
        {
            File3dm rhinoFile = File3dm.Read(filePath);
            if (rhinoFile == null) return 0;

            List<GeometryObject> revitGeometries = new List<GeometryObject>();

            foreach (var obj in rhinoFile.Objects)
            {
                var geometry = obj.Geometry;
                
                if (geometry is Rhino.Geometry.Mesh rhinoMesh)
                {
                    var revitMesh = ConvertRhinoMeshToRevit(rhinoMesh);
                    if (revitMesh != null) revitGeometries.Add(revitMesh);
                }
                else if (geometry is Rhino.Geometry.Brep rhinoBrep)
                {
                    // Rhino3dm không hỗ trợ tạo Mesh mới từ Brep.
                    // Nhưng nếu file 3dm đã từng được view ở chế độ Shaded trong Rhino, 
                    // nó sẽ lưu kèm Render Mesh. Ta có thể trích xuất nó ra.
                    foreach (var face in rhinoBrep.Faces)
                    {
                        var faceMesh = face.GetMesh(Rhino.Geometry.MeshType.Any);
                        if (faceMesh != null)
                        {
                            var rMesh = ConvertRhinoMeshToRevit(faceMesh);
                            if (rMesh != null) revitGeometries.Add(rMesh);
                        }
                    }
                }
                else if (geometry is Rhino.Geometry.Extrusion extrusion)
                {
                    var extMesh = extrusion.GetMesh(Rhino.Geometry.MeshType.Any);
                    if (extMesh != null)
                    {
                        var rMesh = ConvertRhinoMeshToRevit(extMesh);
                        if (rMesh != null) revitGeometries.Add(rMesh);
                    }
                }
            }

            if (!revitGeometries.Any()) return 0;

            using (Transaction t = new Transaction(doc, "Import Rhino 3DM"))
            {
                t.Start();
                
                ElementId categoryId = new ElementId(BuiltInCategory.OST_GenericModel);
                DirectShape ds = DirectShape.CreateElement(doc, categoryId);
                
                ds.ApplicationId = "BIMTOOL";
                ds.ApplicationDataId = "Imported3DM";
                ds.SetShape(revitGeometries);
                
                t.Commit();
            }
            
            return revitGeometries.Count;
        }

        private GeometryObject ConvertRhinoMeshToRevit(Rhino.Geometry.Mesh rhinoMesh)
        {
            TessellatedShapeBuilder builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(false);

            var vertices = rhinoMesh.Vertices;

            foreach (var face in rhinoMesh.Faces)
            {
                List<XYZ> faceVertices = new List<XYZ>();
                
                // Giả định Rhino file ở mm, Revit dùng Feet
                double scale = 1.0 / 304.8;

                faceVertices.Add(new XYZ(vertices[face.A].X * scale, vertices[face.A].Y * scale, vertices[face.A].Z * scale));
                faceVertices.Add(new XYZ(vertices[face.B].X * scale, vertices[face.B].Y * scale, vertices[face.B].Z * scale));
                faceVertices.Add(new XYZ(vertices[face.C].X * scale, vertices[face.C].Y * scale, vertices[face.C].Z * scale));
                
                if (face.IsQuad)
                {
                    faceVertices.Add(new XYZ(vertices[face.D].X * scale, vertices[face.D].Y * scale, vertices[face.D].Z * scale));
                }

                builder.AddFace(new TessellatedFace(faceVertices, ElementId.InvalidElementId));
            }

            builder.CloseConnectedFaceSet();
            builder.Target = TessellatedShapeBuilderTarget.Mesh;
            builder.Fallback = TessellatedShapeBuilderFallback.Mesh;
            builder.Build();

            return builder.GetBuildResult().GetGeometricalObjects().FirstOrDefault();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace RincoNhan.Tools.ImportEXtoLegend.Models
{
    public class StorageSchema
    {
        public static readonly Guid SchemaId = new Guid("4A9C0DE4-9F83-4E87-8A2D-08DF3B514AEB");
        private static readonly string SchemaName = "ImportEXtoLegendStorage";

        public static Schema GetOrCreateSchema()
        {
            Schema schema = Schema.Lookup(SchemaId);
            if (schema != null)
                return schema;

            SchemaBuilder schemaBuilder = new SchemaBuilder(SchemaId);
            schemaBuilder.SetSchemaName(SchemaName);
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);

            schemaBuilder.AddSimpleField("ExcelFilePath", typeof(string));
            schemaBuilder.AddSimpleField("WorksheetName", typeof(string));
            
            // To be able to delete the old items we should store their element Ids
            schemaBuilder.AddArrayField("ElementIds", typeof(ElementId));

            return schemaBuilder.Finish();
        }

        public static DataStorage GetStorageEnity(Document doc, ElementId viewId)
        {
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage))
                .WhereElementIsNotElementType()
                .Cast<DataStorage>()
                .Where(ds => ds.Name == $"ImportEXtoLegend_{viewId.GetIdValue()}");

            return collector.FirstOrDefault();
        }

        public static DataStorage CreateStorageEntity(Document doc, ElementId viewId)
        {
            var existing = GetStorageEnity(doc, viewId);
            if (existing != null) return existing;

            DataStorage storage = DataStorage.Create(doc);
            storage.Name = $"ImportEXtoLegend_{viewId.GetIdValue()}";
            return storage;
        }

        public static void SaveData(Document doc, ElementId viewId, string filePath, string worksheetName, List<ElementId> elementIds)
        {
            var schema = GetOrCreateSchema();
            var storage = GetStorageEnity(doc, viewId) ?? CreateStorageEntity(doc, viewId);

            Entity entity = new Entity(schema);
            entity.Set("ExcelFilePath", filePath);
            entity.Set("WorksheetName", worksheetName);
            
            // Collect the Ids to an IList
            IList<ElementId> idsToStore = new List<ElementId>();
            foreach(var id in elementIds)
            {
                idsToStore.Add(id);
            }
            
            entity.Set("ElementIds", idsToStore);
            storage.SetEntity(entity);
        }

        public static (string filePath, string worksheetName, List<ElementId> elementIds) ReadData(Document doc, ElementId viewId)
        {
            var storage = GetStorageEnity(doc, viewId);
            if (storage == null) return (null, null, new List<ElementId>());

            var schema = GetOrCreateSchema();
            Entity entity = storage.GetEntity(schema);
            if (!entity.IsValid()) return (null, null, new List<ElementId>());

            string path = entity.Get<string>("ExcelFilePath");
            string ws = entity.Get<string>("WorksheetName");
            IList<ElementId> ids = entity.Get<IList<ElementId>>("ElementIds");

            return (path, ws, ids.ToList());
        }
    }
}

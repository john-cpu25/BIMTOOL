# -*- coding: utf-8 -*-
from Autodesk.Revit.UI.Selection import ObjectType
from Autodesk.Revit.DB import *
from System import Math

doc = __revit__.ActiveUIDocument.Document
uidoc = __revit__.ActiveUIDocument

def pick_element_by_category(prompt, category_name):
    ref = uidoc.Selection.PickObject(ObjectType.Element, u"Chọn {}".format(prompt))
    element = doc.GetElement(ref.ElementId)
    if element.Category and element.Category.Name == category_name:
        return element
    else:
        raise Exception(u"Phần tử được chọn không phải '{}'.".format(category_name))

def get_floor_elevation_mm(floor):
    level_id = floor.LevelId
    level = doc.GetElement(level_id)
    elevation = level.Elevation  # in feet

    offset_feet = 0.0
    possible_offset_names = ["Height Offset From Level", "Offset From Level", "Level Offset"]

    for name in possible_offset_names:
        param = floor.LookupParameter(name)
        if param and param.StorageType == StorageType.Double:
            offset_feet = param.AsDouble()
            elevation += offset_feet
            break

    elevation_mm = elevation * 304.8
    return elevation_mm

# Start transaction
t = Transaction(doc, u"Ghi RL STEP (Family)")
t.Start()

try:
    sàn_1 = pick_element_by_category(u"SÀN 1", "Floors")
    sàn_2 = pick_element_by_category(u"SÀN 2", "Floors")
    annotation = pick_element_by_category(u"Generic Annotation", "Generic Annotations")

    z1 = get_floor_elevation_mm(sàn_1)
    z2 = get_floor_elevation_mm(sàn_2)

    delta_mm = Math.Abs(z1 - z2)
    rl_text = u"{:.0f}".format(delta_mm)  # No 'mm' suffix

    param = annotation.LookupParameter("RL STEP")
    if param and not param.IsReadOnly and param.StorageType == StorageType.String:
        param.Set(rl_text)
    else:
        raise Exception(u"Không tìm thấy hoặc không ghi được vào parameter 'RL STEP' (kiểu text, instance).")

except Exception as e:
    # Optional: You can log or print this if needed
    pass

finally:
    t.Commit()

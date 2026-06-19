from Autodesk.Revit.DB import*
from Autodesk.Revit.UI.Selection import*
from Autodesk.Revit.Exceptions import*
import sys
from pyrevit import forms, revit

uidoc = __revit__.ActiveUIDocument
doc = __revit__.ActiveUIDocument.Document
active_view = doc.ActiveView



__doc__ = 'Applies the copied state to plan views on selected sheets. '\
		  'This works in conjunction with the Copy State tool.'

class ViewportFilter(ISelectionFilter):
	def AllowElement(self,e):
		if "Viewport" in str(e.GetType()):
			return True
		else:
			return False


def move_to_source_viewport(vp_result , vp_source):
	view_result = doc.GetElement(vp_result.ViewId)
	view_source = doc.GetElement(vp_source.ViewId)

	#save original boxes
	saved_box_result = view_result.CropBox
	saved_box_source = view_source.CropBox

	#save original box active state
	saved_box_active_result = view_result.CropBoxActive
	saved_box_active_source = view_result.CropBoxActive

	#save original box visible state
	saved_box_visible_result = view_result.CropBoxVisible
	saved_box_visible_source = view_result.CropBoxVisible

	#create new boundingbox that's extremely large
	new_box = BoundingBoxXYZ()
	new_box.Min = XYZ(-10000000000, -10000000000, 0 )
	new_box.Max = XYZ(10000000000, 10000000000, 0 )

	#set new Bounding box to views
	view_result.CropBox = new_box
	view_source.CropBox = new_box

	#make sure CropBox is Active
	view_result.CropBoxActive = True
	view_source.CropBoxActive = True

	#calculate the movement distance diff
	outline_result = vp_result.GetBoxOutline()
	outline_source = vp_source.GetBoxOutline()
	min_result = outline_result.MinimumPoint
	min_source = outline_source.MinimumPoint

	diff = min_source - min_result
	
	#make the move
	ElementTransformUtils.MoveElement(doc, vp_result.Id, diff)

	#return to original states
	view_result.CropBox = saved_box_result
	view_source.CropBox = saved_box_source
	view_result.CropBoxActive = saved_box_active_result
	view_source.CropBoxActive = saved_box_active_source
	view_result.CropBoxVisible = saved_box_visible_result
	view_source.CropBoxVisible = saved_box_visible_source

	return

with forms.WarningBar(title = 'Select Floor Plan Sheets you want to align viewport location:'):
	sheet_selection = forms.select_sheets(title='Select Sheets')

with forms.WarningBar(title = 'Select Viewport as source to copy viewport location:'):
	try:
		ref = uidoc.Selection.PickObject(ObjectType.Element, ViewportFilter(),'Select Viewport as source to copy viewport location:')
	except OperationCanceledException:
		sys.exit()
	vp_source = doc.GetElement(ref)


transaction = Transaction(doc,'Paste Viewport Location')
transaction.Start()

for sheet in sheet_selection:
	viewport_id_col = sheet.GetAllViewports()
	for vport_id in viewport_id_col:
		vport = doc.GetElement(vport_id)
		view_plan = revit.doc.GetElement(vport.ViewId)
		if str(view_plan.GetType()) == "Autodesk.Revit.DB.ViewPlan":
			move_to_source_viewport(vport, vp_source)

transaction.Commit()
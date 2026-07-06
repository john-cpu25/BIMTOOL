from Autodesk.Revit.DB import*
from Autodesk.Revit.UI.Selection import*
from Autodesk.Revit.Exceptions import*
from pyrevit import forms
from rpw.ui.forms import*
import sys
from __units__ import*
from __PT_display__ import*
from __PT_profile__ import*



uidoc = __revit__.ActiveUIDocument
doc = __revit__.ActiveUIDocument.Document
active_view = doc.ActiveView

__title__ = "PT Profile"
__authors__ = "Jason Le"
__doc__ =\
"""
Place chairs to PT profile. Users to provide correct information on High Points, Low Point, End Conditions and Soffit Shanges

"""

def UI_get_high_point(point_string):
	if point_string == "1st":
		title_UI = "Pick the First High Point:"
	if point_string == "2nd":
		title_UI = "Pick the Second High Point:"
	if point_string == "next":
		title_UI = "Pick the Next High Point:"
	with forms.WarningBar(title = title_UI):
		try:
			high_pt_location = uidoc.Selection.PickPoint(title_UI)
		except OperationCanceledException:
			if _is_continue_from_last_span == False:
				sys.exit()
			else:
				return None

	try:
		high_pt_condition = forms.CommandSwitchWindow.show(['Live/Dead End', 'Continuous'],
															message='Select End Condition:',)
	except Exception as e:
		forms.alert(str(e), exitscript=True)
		sys.exit()
	if high_pt_condition == 'Live/Dead End':
		high_pt_condition = "end"
	elif high_pt_condition == 'Continuous':
		high_pt_condition = "continuous"
	else:
		if _is_continue_from_last_span == False:
			sys.exit()
		else:
			return None


	try:
		high_pt_height = forms.ask_for_string(
									default='150',
									prompt='Enter Height at this High Point:\n'
											'(For Live/Dead End, enter height to Centre Line of PT)',
									title='High Point')

		high_pt_height = float(high_pt_height)
	except SystemError as e:
		if _is_continue_from_last_span == False:
			sys.exit()
		else:
			return None
	except Exception as e:
		if _is_continue_from_last_span == False:
			forms.alert(str(e), exitscript=True)
		else:
			forms.alert(str(e), exitscript=False)
			return None

	high_point_input_matrix = [high_pt_location,high_pt_height,high_pt_condition]
	return high_point_input_matrix

def UI_get_low_point():
	with forms.WarningBar(title = "Low Point"):
		try:
			low_pt_height = forms.ask_for_string(
										default='0',#Fix default='30'
										prompt='Enter Height at Low Point:\n'
												'(Enter 0 (Zero) for Straight PT)',
										title='Low Point')
			low_pt_height = float(low_pt_height)
		except SystemError as e:
			if _is_continue_from_last_span == False:
				sys.exit()
			else:
				return None
		except Exception as e:
			if _is_continue_from_last_span == False:
				forms.alert(str(e), exitscript=True)
			else:
				forms.alert(str(e), exitscript=False)
				return None
	return low_pt_height


def soffit_change_data(high_point_1_view_coord, high_point_2_view_coord):
	span_line = Line.CreateBound(high_point_1_view_coord, high_point_2_view_coord)
	soffit_change_list = []
	high_pt_1_location, high_pt_2_location = translate_rotate_axes_2pts(high_point_1_view_coord, high_point_2_view_coord)
	first_point = 0.0
	end_point = high_pt_2_location.X
	elevation_change_value = 0
	while True:
		dialog_soffit_change = TaskDialog('Do you want make changes to the soffit?\n\n\
												Reminder:\n\
												The locations of soffit steps must be orderly picked\
												in the same direction from 1st High Point to 2nd High Point',
									title_prefix=False,
									buttons=['Yes', 'No'],
									show_close=True)
		dialog_soffit_change.show()
		if str(dialog_soffit_change.result) in ["No", "Cancel"]:
			break
		else:
			with forms.WarningBar(title="Pick Point where Soffit Change Happens"):
				try:
					soffit_location_XYZ = uidoc.Selection.PickPoint(
							"Pick Point where Soffit Change Happens")
					soffit_location_XYZ = XYZ(soffit_location_XYZ.X, soffit_location_XYZ.Y, 0)
				except OperationCanceledException:
					break
			_check_close_form = False
			while True:
				try:
					soffit_form_components = [	
						Label('You Must Tick 1 Box Only:'),
						CheckBox('step_up', 'Soffit Step Up',default=False),
						CheckBox('step_down', 'Soffit Step Down',default=False),
						Separator(),
						Label('Soffit Change Value (Positive Number):'),
						TextBox('elevation_change'),
						Separator(),
						Button('OK')
									]
					soffit_form = FlexForm('Soffit Change', soffit_form_components)
					soffit_form.show()
					_is_step_up = soffit_form.values['step_up']
					_is_step_down = soffit_form.values['step_down']
					_check_input_number = True
					try:
						elevation_change = int(soffit_form.values['elevation_change'])
					except:
						_check_input_number = False

					if _is_step_up != _is_step_down and _check_input_number == True:
						if elevation_change > 0:
							break
				except KeyError:
					_check_close_form = True
					break

			if _check_close_form == True:
				break
			else:
				if _is_step_up == True:
					elevation_change = elevation_change
				else:
					elevation_change = -elevation_change

				soffit_project_point_on_span_line = span_line.Project(soffit_location_XYZ).XYZPoint
				high_pt_1_location, soffit_pt_location = translate_rotate_axes_2pts(high_point_1_view_coord, soffit_project_point_on_span_line)
				second_point = soffit_pt_location.X
				soffit_change_dict = {(first_point,second_point):elevation_change_value}
				soffit_change_list.append(soffit_change_dict)
				first_point = second_point
				elevation_change_value = elevation_change_value + elevation_change

				# soffit_change_dict = {x_length_to_soffit_point, elevation_change}\
	soffit_change_dict = {(first_point,end_point):elevation_change_value}
	soffit_change_list.append(soffit_change_dict)
		
	return soffit_change_list


def translate_rotate_axes_2pts(pt1,pt2):
	#change coordiates of point (x1,y1) to (0,0) and (x2,y2) to (L,0) with L is distance between 2 points
	pt1 = Point(ft2mm(pt1.X),ft2mm(pt1.Y))
	pt2 = Point(ft2mm(pt2.X),ft2mm(pt2.Y))
	h = pt1.X
	k = pt1.Y
	L = length_2pts(pt1,pt2)
	pt1 = Point(pt1.X - h, pt1.Y-k)
	pt2 = Point(L,0.0)
	return pt1, pt2


def generate_two_high_pts_for_calculation(high_point_1_input_matrix,high_point_2_input_matrix):
	high_pt_1_location = high_point_1_input_matrix[0]
	high_pt_2_location = high_point_2_input_matrix[0]
	high_pt_1_location, high_pt_2_location = translate_rotate_axes_2pts(high_pt_1_location, high_pt_2_location)
	high_pt_1 = High_Point(high_pt_1_location.X, high_point_1_input_matrix[1], high_point_1_input_matrix[2])
	high_pt_2 = High_Point(high_pt_2_location.X, high_point_2_input_matrix[1], high_point_2_input_matrix[2])
	return high_pt_1, high_pt_2


def place_PT_markers(high_point_1_view_coord,high_point_2_view_coord, high_pt_1,high_pt_2,low_pt_height, soffit_change_list,chair_max_spacing):
	length = high_pt_2.X
	#calculate spacing of drape markers
	if length >=8500:
		gap_number = int(round(length/(chair_max_spacing)))
	elif length >= 4950 and length < 8500:
		gap_number = int(round(length/(0.9 * chair_max_spacing)))
	else:
		gap_number = int(round(length/(0.85 * chair_max_spacing)))
	if gap_number % 2 == 1 and high_pt_1.Y == high_pt_2.Y and high_pt_1.condition == high_pt_2.condition:
		gap_number = gap_number + 1

	gap_length = length/gap_number

	min_pt_height_found = max(high_pt_1.Y,high_pt_2.Y)
	min_pt_x_delta = high_pt_2.X

	PT_marker_list = []
	PT_marker_x_coordinate_list = []
	#rotate the marker so that the text is perpendicular to the direction of PT
	rotate_angle_marker = calculate_rotate_angle_marker(high_point_1_view_coord, high_point_2_view_coord)


	if len(soffit_change_list) == 1:
		#fomulate parabolas S1 S2 S3 (short of sector 1, 2, 3)
		#with sector 1 being from first high point to first inflection point
		#sector 2 being from first inflection point to second inflection point
		#sector 3 being from second inflection point to second high point
		S1, S2, S3, inflection_pt1, inflection_pt2, low_pt = PT_profile(high_pt_1 , high_pt_2, low_pt_height)
	else:
		original_high_pt_1_height = high_pt_1.Y
		original_high_pt_2_height = high_pt_2.Y
		for soffit_change in soffit_change_list:
			lower_bound = soffit_change.keys()[0][0]
			upper_bound = soffit_change.keys()[0][1]
			high_pt_1_soffit_elevation = soffit_change_list[0].values()[0]
			high_pt_2_soffit_elevation = soffit_change_list[-1].values()[0]
			selected_elevation_change = soffit_change.values()[0]
			#adjust height of end points based on which soffit region is assummed base of flat slab
			if soffit_change == soffit_change_list[0]:
				high_pt_2.Y = high_pt_2.Y + (high_pt_2_soffit_elevation - selected_elevation_change)
			elif soffit_change == soffit_change_list[-1]:
				high_pt_1.Y = high_pt_1.Y + (high_pt_1_soffit_elevation - selected_elevation_change)
			else:
				high_pt_1.Y = high_pt_1.Y + (high_pt_1_soffit_elevation - selected_elevation_change)
				high_pt_2.Y = high_pt_2.Y + (high_pt_2_soffit_elevation - selected_elevation_change)
			if low_pt_height > high_pt_1.Y or low_pt_height > high_pt_2.Y:
				continue
			S1, S2, S3, inflection_pt1, inflection_pt2, low_pt = PT_profile(high_pt_1 , high_pt_2, low_pt_height)
			high_pt_1.Y = original_high_pt_1_height
			high_pt_2.Y = original_high_pt_2_height
			if low_pt != None:
				if lower_bound <= low_pt.X and upper_bound >= low_pt.X:
					break
	
	for i in range (0,gap_number+1):
		x = gap_length*i
		if len(soffit_change_list) == 1:
			drape_elevation_change = 0
		else:
			for soffit_change in soffit_change_list:
				lower_bound = soffit_change.keys()[0][0]
				upper_bound = soffit_change.keys()[0][1]
				if lower_bound <= x and upper_bound >= x:
					soffit_elevation = soffit_change.values()[0]
					drape_elevation_change = soffit_elevation - selected_elevation_change

		if len(S1) == 0 and len(S3) == 0:
			if len(S2) == 3:
				y = quadratic_y_value(S2,x)
			if len(S2) == 2:
				y = linear_y_value(S2,x)

		if len(S1) > 0 and len(S3)==0:
			if x <= inflection_pt1.X:
				y = quadratic_y_value(S1,x)
			else:
				y = quadratic_y_value(S2,x)

		if len(S1) == 0 and len(S3) > 0:
			if x < inflection_pt2.X:
				y = quadratic_y_value(S2,x)
			else:
				y = quadratic_y_value(S3,x)

		if len(S1) > 0 and len(S3) >0:
			if x <= inflection_pt1.X:
				y = quadratic_y_value(S1,x)
			elif x > inflection_pt1.X and x < inflection_pt2.X:
				y = quadratic_y_value(S2,x)
			else:
				y = quadratic_y_value(S3,x)
		y = round_nearest_5(y) - drape_elevation_change


		if low_pt_height > 0:
			if y < min_pt_height_found:
				min_pt_height_found = y
			if abs(x - low_pt.X) < min_pt_x_delta:
				min_pt_x_delta = abs(x-low_pt.X)
				min_pt_x_select = x


		if x == high_pt_1.X:
			marker_pt_type = high_pt_1.condition
		elif x == high_pt_2.X:
			marker_pt_type = high_pt_2.condition
		else:
			marker_pt_type = "intermediate"
		# print(x,y,marker_pt_type)
		if _is_continue_from_last_span == False or x != 0:
			PT_marker_x_coordinate_list.append(x)
			#calculate real coordiates of marker points based on the distance x along the PT from first picked high point
			marker_coordinate_on_view_X = high_point_1_view_coord.X + (float(x)/length)*(high_point_2_view_coord.X - high_point_1_view_coord.X)
			marker_coordinate_on_view_Y = high_point_1_view_coord.Y + (float(x)/length)*(high_point_2_view_coord.Y - high_point_1_view_coord.Y)

			PT_marker_location = XYZ(marker_coordinate_on_view_X, marker_coordinate_on_view_Y, 0)
			PT_marker = doc.Create.NewFamilyInstance(PT_marker_location,PT_marker_fam_symbol,active_view)
			axis = Line.CreateBound(PT_marker_location, XYZ(marker_coordinate_on_view_X,marker_coordinate_on_view_Y,1))
			#rotate the marker so that the text is perpendicular to the direction of PT
			PT_marker.Location.Rotate(axis,rotate_angle_marker)

			if marker_pt_type == "end":
				y = y + 10
				PT_marker.LookupParameter("Centre Line Point").Set(1)
				PT_marker.LookupParameter("Main Point").Set(0)
				PT_marker.LookupParameter("Intermediate Point").Set(0)
			if marker_pt_type == "continuous":
				PT_marker.LookupParameter("Centre Line Point").Set(0)
				PT_marker.LookupParameter("Main Point").Set(1)
				PT_marker.LookupParameter("Intermediate Point").Set(0)
			if marker_pt_type == "intermediate":
				PT_marker.LookupParameter("Centre Line Point").Set(0)
				PT_marker.LookupParameter("Main Point").Set(0)
				PT_marker.LookupParameter("Intermediate Point").Set(1)
			PT_marker.LookupParameter("PT Drape Height").Set(y)
			PT_marker_list.append(PT_marker)

	#find out which marker point is closest to the real low point location and assign that marker to low point
	if low_pt_height > 0:
		for i in range(0,len(PT_marker_list)):
			if min_pt_x_select == PT_marker_x_coordinate_list[i]:
				PT_marker_list[i].LookupParameter("Centre Line Point").Set(0)
				PT_marker_list[i].LookupParameter("Main Point").Set(1)
				PT_marker_list[i].LookupParameter("Intermediate Point").Set(0)
				if PT_marker_list[i].LookupParameter("PT Drape Height").AsInteger() != int(low_pt_height):
					PT_marker_list[i].LookupParameter("PT Drape Height").Set(low_pt_height)


def calculate_rotate_angle_marker(high_point_1_view_coord, high_point_2_view_coord):
	if abs(high_point_2_view_coord.X - high_point_1_view_coord.X) < 0.001:
		angle = 3.14159/2.0
	elif abs(high_point_2_view_coord.Y - high_point_1_view_coord.Y) < 0.001:
		angle = 0
	else:
		angle = math.atan((high_point_2_view_coord.Y - high_point_1_view_coord.Y)/(high_point_2_view_coord.X - high_point_1_view_coord.X))
	if angle > 0:
		angle = angle + 3.14159
	return angle



"""
###################################################################################
"""



chair_max_spacing = 1000


# transaction = Transaction(doc,'PT marker family load')
# transaction.Start()
PT_marker_fam_symbol = PT_marker_load_family()

# transaction.Commit()

_is_continue_from_last_span = False

high_point_1_input_matrix = UI_get_high_point("1st")	# [coordiates on view (x,y,z) , height ot high pt, End condition]

low_pt_height = UI_get_low_point()

high_point_2_input_matrix = UI_get_high_point("2nd")	# [coordiates on view (x,y,z) , height ot high pt, End condition]

high_point_1_view_coord = high_point_1_input_matrix[0]
high_point_2_view_coord = high_point_2_input_matrix[0]
high_pt_1, high_pt_2 = generate_two_high_pts_for_calculation(high_point_1_input_matrix, high_point_2_input_matrix)

soffit_change_list = soffit_change_data(high_point_1_view_coord, high_point_2_view_coord)


transaction = Transaction(doc,"Place PT markers")
transaction.Start()
place_PT_markers(high_point_1_view_coord,high_point_2_view_coord, high_pt_1,high_pt_2,low_pt_height,soffit_change_list,chair_max_spacing)
transaction.Commit()

while True:
	if high_pt_2.condition == "continuous":
		dialog_next_span = TaskDialog('Do you want to continue to Next Span?',
								title_prefix=False,
								buttons=['Yes', 'No'],
								show_close=True)
		dialog_next_span.show()

		if str(dialog_next_span.result) in ["No", "Cancel"]:
			break
		else:
			_is_continue_from_last_span = True
			high_point_1_input_matrix = high_point_2_input_matrix
			low_pt_height = UI_get_low_point()
			if low_pt_height == None:	
				#None means the user has pressed Esc or made mistake during UI_get_low_point()
				break
			
			high_point_2_input_matrix = UI_get_high_point("next")
			if high_point_2_input_matrix == None:
				#None means the user has pressed Esc or made mistake during UI_get_high_point()
				break

			high_point_1_view_coord = high_point_1_input_matrix[0]
			high_point_2_view_coord = high_point_2_input_matrix[0]
			
			high_pt_1, high_pt_2 = generate_two_high_pts_for_calculation(high_point_1_input_matrix, high_point_2_input_matrix)
			soffit_change_list = soffit_change_data(high_point_1_view_coord, high_point_2_view_coord)

			transaction = Transaction(doc,"Place PT markers")
			transaction.Start()
			place_PT_markers(high_point_1_view_coord,high_point_2_view_coord, high_pt_1,high_pt_2,low_pt_height,soffit_change_list,chair_max_spacing)
			transaction.Commit()
	else:
		break
	

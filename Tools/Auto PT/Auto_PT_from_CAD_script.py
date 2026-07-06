# -*- coding: utf-8 -*-
from Autodesk.Revit.DB import *
from Autodesk.Revit.UI.Selection import *
from Autodesk.Revit.Exceptions import *
from pyrevit import forms
from rpw.ui.forms import *
import sys
import math

from __units__ import *
from __PT_display__ import *
from __PT_profile__ import *

uidoc = __revit__.ActiveUIDocument
doc = __revit__.ActiveUIDocument.Document
active_view = doc.ActiveView

__title__ = "Auto PT from CAD Link"
__authors__ = "Jason Le, Nhan, AI Assistant"
__doc__ = """
Automatically read PT layout lines from a linked CAD file and place chairs/markers.
"""

def get_cad_link():
    try:
        ref = uidoc.Selection.PickObject(ObjectType.Element, "Select CAD Link containing PT layout")
        return doc.GetElement(ref)
    except OperationCanceledException:
        sys.exit()

def get_cad_layers(cad_instance):
    layers = set()
    geom_elem = cad_instance.get_Geometry(Options())
    if geom_elem:
        for geom_obj in geom_elem:
            if isinstance(geom_obj, GeometryInstance):
                inst_geom = geom_obj.GetInstanceGeometry()
                for g in inst_geom:
                    if isinstance(g, Curve) or isinstance(g, PolyLine):
                        gs = doc.GetElement(g.GraphicsStyleId)
                        if gs and gs.GraphicsStyleCategory:
                            layers.add(gs.GraphicsStyleCategory.Name)
    return sorted(list(layers))

def get_polylines_from_layer(cad_instance, layer_name):
    paths = []
    geom_elem = cad_instance.get_Geometry(Options())
    if geom_elem:
        for geom_obj in geom_elem:
            if isinstance(geom_obj, GeometryInstance):
                transform = geom_obj.Transform
                inst_geom = geom_obj.GetInstanceGeometry()
                for g in inst_geom:
                    gs = doc.GetElement(g.GraphicsStyleId)
                    if gs and gs.GraphicsStyleCategory and gs.GraphicsStyleCategory.Name == layer_name:
                        pts = []
                        if isinstance(g, Curve):
                            pts = [g.GetEndPoint(0), g.GetEndPoint(1)]
                        elif isinstance(g, PolyLine):
                            pts = list(g.GetCoordinates())
                        
                        if pts:
                            # Apply transform if needed, though GetInstanceGeometry usually has coordinates in project space
                            paths.append(pts)
    return paths

# Reusing some functions from original script
def translate_rotate_axes_2pts(pt1,pt2):
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

def place_PT_markers(high_point_1_view_coord,high_point_2_view_coord, high_pt_1,high_pt_2,low_pt_height, soffit_change_list,chair_max_spacing, PT_marker_fam_symbol):
    length = high_pt_2.X
    if length <= 0:
        return
    if length >=8500:
        gap_number = int(round(length/(chair_max_spacing)))
    elif length >= 4950 and length < 8500:
        gap_number = int(round(length/(0.9 * chair_max_spacing)))
    else:
        gap_number = int(round(length/(0.85 * chair_max_spacing)))
    if gap_number == 0:
        gap_number = 1
    if gap_number % 2 == 1 and high_pt_1.Y == high_pt_2.Y and high_pt_1.condition == high_pt_2.condition:
        gap_number = gap_number + 1

    gap_length = length/gap_number

    min_pt_height_found = max(high_pt_1.Y,high_pt_2.Y)
    min_pt_x_delta = high_pt_2.X
    min_pt_x_select = 0

    PT_marker_list = []
    PT_marker_x_coordinate_list = []
    rotate_angle_marker = calculate_rotate_angle_marker(high_point_1_view_coord, high_point_2_view_coord)

    if len(soffit_change_list) == 1:
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
        drape_elevation_change = 0
        if len(soffit_change_list) > 1:
            for soffit_change in soffit_change_list:
                lower_bound = soffit_change.keys()[0][0]
                upper_bound = soffit_change.keys()[0][1]
                if lower_bound <= x and upper_bound >= x:
                    soffit_elevation = soffit_change.values()[0]
                    drape_elevation_change = soffit_elevation - selected_elevation_change

        y = 0
        if len(S1) == 0 and len(S3) == 0:
            if len(S2) == 3: y = quadratic_y_value(S2,x)
            if len(S2) == 2: y = linear_y_value(S2,x)
        elif len(S1) > 0 and len(S3)==0:
            if x <= inflection_pt1.X: y = quadratic_y_value(S1,x)
            else: y = quadratic_y_value(S2,x)
        elif len(S1) == 0 and len(S3) > 0:
            if x < inflection_pt2.X: y = quadratic_y_value(S2,x)
            else: y = quadratic_y_value(S3,x)
        elif len(S1) > 0 and len(S3) >0:
            if x <= inflection_pt1.X: y = quadratic_y_value(S1,x)
            elif x > inflection_pt1.X and x < inflection_pt2.X: y = quadratic_y_value(S2,x)
            else: y = quadratic_y_value(S3,x)
            
        y = round_nearest_5(y) - drape_elevation_change

        if low_pt_height > 0 and low_pt != None:
            if y < min_pt_height_found:
                min_pt_height_found = y
            if abs(x - low_pt.X) < min_pt_x_delta:
                min_pt_x_delta = abs(x-low_pt.X)
                min_pt_x_select = x

        if abs(x - high_pt_1.X) < 0.1: marker_pt_type = high_pt_1.condition
        elif abs(x - high_pt_2.X) < 0.1: marker_pt_type = high_pt_2.condition
        else: marker_pt_type = "intermediate"

        PT_marker_x_coordinate_list.append(x)
        marker_coordinate_on_view_X = high_point_1_view_coord.X + (float(x)/length)*(high_point_2_view_coord.X - high_point_1_view_coord.X)
        marker_coordinate_on_view_Y = high_point_1_view_coord.Y + (float(x)/length)*(high_point_2_view_coord.Y - high_point_1_view_coord.Y)

        PT_marker_location = XYZ(marker_coordinate_on_view_X, marker_coordinate_on_view_Y, 0)
        PT_marker = doc.Create.NewFamilyInstance(PT_marker_location,PT_marker_fam_symbol,active_view)
        axis = Line.CreateBound(PT_marker_location, XYZ(marker_coordinate_on_view_X,marker_coordinate_on_view_Y,1))
        PT_marker.Location.Rotate(axis,rotate_angle_marker)

        if marker_pt_type == "end":
            y = y + 10
            PT_marker.LookupParameter("Centre Line Point").Set(1)
            PT_marker.LookupParameter("Main Point").Set(0)
            PT_marker.LookupParameter("Intermediate Point").Set(0)
        elif marker_pt_type == "continuous":
            PT_marker.LookupParameter("Centre Line Point").Set(0)
            PT_marker.LookupParameter("Main Point").Set(1)
            PT_marker.LookupParameter("Intermediate Point").Set(0)
        elif marker_pt_type == "intermediate":
            PT_marker.LookupParameter("Centre Line Point").Set(0)
            PT_marker.LookupParameter("Main Point").Set(0)
            PT_marker.LookupParameter("Intermediate Point").Set(1)
            
        PT_marker.LookupParameter("PT Drape Height").Set(y)
        PT_marker_list.append(PT_marker)

    if low_pt_height > 0:
        for i in range(0,len(PT_marker_list)):
            if min_pt_x_select == PT_marker_x_coordinate_list[i]:
                PT_marker_list[i].LookupParameter("Centre Line Point").Set(0)
                PT_marker_list[i].LookupParameter("Main Point").Set(1)
                PT_marker_list[i].LookupParameter("Intermediate Point").Set(0)
                if PT_marker_list[i].LookupParameter("PT Drape Height").AsInteger() != int(low_pt_height):
                    PT_marker_list[i].LookupParameter("PT Drape Height").Set(low_pt_height)

def main():
    cad_link = get_cad_link()
    layers = get_cad_layers(cad_link)
    
    if not layers:
        forms.alert("No layers found in the CAD link.")
        sys.exit()
        
    selected_layer = forms.SelectFromList.show(layers, title="Select Layer containing PT Layout (PolyLines/Lines)")
    if not selected_layer:
        sys.exit()
        
    paths = get_polylines_from_layer(cad_link, selected_layer)
    if not paths:
        forms.alert("No valid paths found on layer: " + selected_layer)
        sys.exit()
        
    components = [
        Label('Default Settings for all PT Spans extracted:'),
        Separator(),
        Label('Default High Point Height (e.g. 150):'),
        TextBox('hp_height', default='150'),
        Label('Default Low Point Height (e.g. 0 or 30):'),
        TextBox('lp_height', default='0'),
        Separator(),
        Button('Generate All')
    ]
    form = FlexForm('Global PT Settings', components)
    form.show()
    
    if 'hp_height' not in form.values:
        sys.exit()
        
    hp_height = float(form.values['hp_height'])
    lp_height = float(form.values['lp_height'])
    chair_max_spacing = 1000

    PT_marker_fam_symbol = PT_marker_load_family()

    with Transaction(doc, "Auto PT from CAD") as t:
        t.Start()
        
        for pts in paths:
            if len(pts) < 2:
                continue
                
            for i in range(len(pts) - 1):
                high_point_1_view_coord = pts[i]
                high_point_2_view_coord = pts[i+1]
                
                condition1 = "end" if i == 0 else "continuous"
                condition2 = "end" if i == len(pts) - 2 else "continuous"
                
                hp1_matrix = [high_point_1_view_coord, hp_height, condition1]
                hp2_matrix = [high_point_2_view_coord, hp_height, condition2]
                
                high_pt_1, high_pt_2 = generate_two_high_pts_for_calculation(hp1_matrix, hp2_matrix)
                
                span_line = Line.CreateBound(high_point_1_view_coord, high_point_2_view_coord)
                high_pt_1_loc, high_pt_2_loc = translate_rotate_axes_2pts(high_point_1_view_coord, high_point_2_view_coord)
                soffit_change_list = [{(0.0, high_pt_2_loc.X): 0}]
                
                place_PT_markers(high_point_1_view_coord, high_point_2_view_coord, high_pt_1, high_pt_2, lp_height, soffit_change_list, chair_max_spacing, PT_marker_fam_symbol)
                
        t.Commit()
        
    forms.alert("Successfully generated PT markers for {} path elements.".format(len(paths)))

if __name__ == '__main__':
    main()

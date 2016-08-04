﻿AddVbFile "dmt.vb"                      'DMT
AddVbFile "40part_export.vb"            'PartExport.part_export
AddVbFile "50partrev_export.vb"         'PartRevExport.part_rev_export
AddVbFile "60partplant_export.vb"       'PartPlantExport.part_plant_export
AddVbFile "species_list.vb"             'Species.species_list
AddVbFile "species_common.vb"           'SpeciesOps.select_active_part
AddVbFile "quoting_common.vb"           'QuotingOps.generate_desc
AddVbFile "inventor_common.vb"          'InventorOps.get_param_set

Sub Main()
    'Pull latest data from Epicor
    'this data shouldn't change often, so the rule shouldn't need to be called often
    'DMT.dmt_export()

    'populate the PartNumberToUse param multi-value with the activated part numbers
    Dim app As Inventor.Application = ThisApplication
    Dim inv_params As UserParameters = InventorOps.get_param_set(app)

    Dim form_result As FormResult = FormResult.OK

    'setup the parameters this module needs
    iLogicVb.RunExternalRule("10multi_value.vb")

    'select the part to work on (placed in "PartNumberToUse" Inventor User Parameter)
    Dim materials_only As Boolean = inv_params.Item("MaterialsOnly").Value
    form_result = SpeciesOps.select_active_part(app, inv_params, Species.species_list, _
                                                iLogicForm, iLogicVb, MultiValue, materials_only)
    If form_result = FormResult.Cancel OrElse form_result = FormResult.None Then
        Return
    End If

    'set some parameters based on the type of the selected part
    Dim design_props As PropertySet = app.ActiveDocument.PropertySets.Item("Design Tracking Properties")
    Dim part As Tuple(Of String, String, String) = SpeciesOps.unpack_pn(inv_params.Item("PartNumberToUse").Value)
    Dim part_species As String = part.Item3
    Dim is_part As Boolean = inv_params.Item("ActiveIsPart").Value
    If is_part Then
        inv_params.Item("Description").Value = design_props.Item("Description").Value
    Else
        Try
            inv_params.Item("Description").Value = QuotingOps.generate_desc(part_species, inv_params)
            inv_params.Item("UsePartRev").Value = False
            inv_params.Item("TrackSerialNum").Value = False
        Catch ex As Exception
            MsgBox("Warning: the fields for this raw material haven't been setup. " & _
                   "Try running the Quoting Spreadsheet export first.")
            Return
        End Try
    End If

    'Call the other rules in order
    form_result = iLogicForm.ShowGlobal("epicor_20part_properties", FormMode.Modal).Result
    If form_result = FormResult.Cancel OrElse form_result = FormResult.None Then
        Return
    End If

    form_result = check_logic(app)
    If form_result = FormResult.Cancel OrElse form_result = FormResult.None Then
        Return
    End If

    iLogicVb.RunExternalRule("30set_props.vb")

    'if the flag for the current part/mat shows it's already been exported, abort
    Dim flag As String = "Exported"
    If is_part Then
        flag = flag & "Part"
    Else
        flag = flag & "Mat"
    End If
    flag = flag & Replace(part_species, "-", "4")
    If inv_params.Item(flag).Value Then
        MsgBox("Part/species combination """ & part.Item1 & "/" & part_species & _
               """ has already been exported into Epicor from this document. Aborting...")
        Return
    End If

    'if part export fails, abort - this will usually mean the part is already
    'in the DB and so the straight add operation failed
    Dim dmt_obj As New DMT()
    Dim ret_value = PartExport.part_export(app, inv_params, dmt_obj)
    If ret_value = 0 Then
        PartRevExport.part_rev_export(app, inv_params, dmt_obj)
        PartPlantExport.part_plant_export(app, inv_params, dmt_obj)
        inv_params.Item(flag).Value = True
    ElseIf ret_value = -1 Then
        MsgBox("Error: DMT timed out. Aborting...")
    Else
        MsgBox("Warning: this part is already present in Epicor. Aborting...")
    End If

    'TODO: display message box about DMT state - maybe last 3 lines of logfile
End Sub

'validate the form logic, and return a form result (if reentry required) that
' lets the user abort
Function check_logic(ByRef app As Inventor.Application) As FormResult
    'set a few parameters depending on data entered in first form
    Dim inv_params As UserParameters = InventorOps.get_param_set(app)
    Dim design_props As PropertySet = app.ActiveDocument.PropertySets.Item("Design Tracking Properties")

    Dim form_result As FormResult = FormResult.OK

    Dim fails_validation As Boolean = False
    Dim required_params As New Dictionary(Of String, String) From _
            {{"ProdCode", "Group"}, {"ClassID", "Class"}}

    'do the actual validation - there aren't many keyboard-entered fields, so
    'the most important thing to check for is that values were selected from
    'the dropdowns
    Do
        Dim error_log As String = ""
        Dim description As String = inv_params.Item("Description").Value

        Dim appr_date, null_date As Date
        appr_date = design_props.Item("Engr Date Approved").Value
        null_date = #1/1/1601#

        If String.IsNullOrEmpty(description) Then
            error_log = error_log & System.Environment.Newline & _
                        "- Enter a description"
            fails_validation = True
        End If

        For Each kvp As KeyValuePair(Of String, String) In required_params
            If String.IsNullOrEmpty(inv_params.Item(kvp.Key).Value) Then
                error_log = error_log & System.Environment.Newline & _
                            "- Select a value for " & kvp.Value
                fails_validation = True
            End If
        Next

        If appr_date = null_date Then
            error_log = error_log & System.Environment.Newline & _
                        "- Select an approval date"
            fails_validation = True
        End If

        'set the flag to false if no errors were detected in THIS iteration
        If String.IsNullOrEmpty(error_log) Then
            fails_validation = False
        End If

        If fails_validation Then
            MsgBox("Please correct the following problems with the part info:" & _
                   error_log)
            form_result = iLogicForm.ShowGlobal("epicor_20part_properties", FormMode.Modal).Result

            If form_result = FormResult.Cancel OrElse form_result = FormResult.None Then
                Exit Do
            End If
        End If
    Loop While fails_validation

    Return form_result
End Function

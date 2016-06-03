AddVbFile "dmt.vb"

'set iProperties with values the user has defined in a form
'note: these values will mostly be the IDs the Epicor DMT is expecting rather
'      than the human-readable strings
Sub Main()
    'list of parameters that need to be converted to iProperties
    Dim inv_doc As Document = ThisApplication.ActiveDocument
    Dim inv_params As UserParameters = inv_doc.Parameters.UserParameters

    'mappings for human-readable values (i.e. in the dropdown boxes) -> keys
    'only necessary for ProdCode and ClassID
    Dim ProdCodeMap As Dictionary(Of String, String) = fetch_list_mappings("ProdCode.csv")
    Dim ClassIDMap As Dictionary(Of String, String) = fetch_list_mappings("ClassID.csv")
    Dim VendorNumMap As Dictionary(Of String, String) = fetch_list_mappings("VendorNum.csv")

    'TODO: map approving engineers to Epicor IDs?

    For i = 1 To inv_params.Count
        'if Epicor requires a short ID, convert the human-readable value via
        'the appropriate mapping (see above)
        'required for: ProdCode, ClasID
        Dim param As Parameter = inv_params.Item(i)
        Dim param_name As String = param.Name
        Dim param_value = param.Value

        If StrComp(param_name, "ProdCode") = 0 Then
            param_value = ProdCodeMap(param_value)
        Else If StrComp(param_name, "ClassID") = 0 Then
            param_value = ClassIDMap(param_value)
        Else If StrComp(param_name, "VendorNum") = 0 Then
            param_value = VendorNumMap(param_value)
        Else If StrComp(param_name, "MfgComment") = 0 Then
            'note: Epicor MfgComment and PurComment fields supports up to 16000 chars,
            'and commas need to be stripped to avoid messing up the CSV
            param_value = Replace(param_value, ",", "")
            param_value = Left(param_value, 16000)
        Else If StrComp(param_name, "PurComment") = 0 Then
            param_value = Replace(param_value, ",", "")
            param_value = Left(param_value, 16000)
        Else If StrComp(param_name, "RevDescription") = 0 Then
            param_value = Replace(param_value, ",", "")
            param_value = Left(param_value, 16000)
        End If

        updateProp(param_name, param_value)

        inv_doc.Update
    Next
End Sub

Sub updateProp(ByVal n As String, ByVal paramVal As Object)
    'get the custom property collection
    Dim invDoc As Document = ThisApplication.ActiveDocument
    Dim invCustomPropertySet As PropertySet 
    invCustomPropertySet = invDoc.PropertySets.Item("Inventor User Defined Properties")

    ' Attempt to get existing custom property
    On Error Resume Next
    Dim invProp
    invProp = invCustomPropertySet.Item(n)
    If Err.Number <> 0 Then
        'Failed to get the property, which means it doesn't already exist,
        'so we'll create it
        invCustomPropertySet.Add(paramVal, n)
    Else
        'got the property so update the value
        invProp.value = paramVal
    End If
End Sub

'Map the description in the parameter to the DB friendly ID expected by Epicor
Function fetch_list_mappings(ByVal f As String) As Dictionary(Of String, String)
    Dim file_name As String = DMT.dmt_working_path & "ref\" & f
    Dim mapping As New Dictionary(Of String, String)

    Using csv_reader As New FileIO.TextFieldParser(file_name)
        csv_reader.TextFieldType = FileIO.FieldType.Delimited
        csv_reader.SetDelimiters(",")

        Dim current_row As String()
        Dim first_line As Boolean = True
        While Not csv_reader.EndOfData
            Try
                current_row = csv_reader.ReadFields()
            Catch ex As FileIO.MalformedLineException
                Debug.Write("CSV contained invalid line:" & ex.Message)
            End Try

            If first_line Then
                'skip headers
                first_line = False
            Else
                mapping.Add(current_row(0), current_row(1))
            End If
        End While
    End Using

    Return mapping
End Function
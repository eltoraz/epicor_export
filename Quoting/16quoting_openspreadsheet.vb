AddVbFile "parameters.vb"           'ParameterOps.get_param_set

'wrapper rule to launch Excel on the spreadsheet selected
' and stored in the "QuotingSpreadsheet" parameter
Sub Main()
    Dim inv_app As Inventor.Application = ThisApplication
    Dim inv_params As UserParameters = ParameterOps.get_param_set(inv_app)
    Dim filename As String = inv_params.Item("QuotingSpreadsheet").Value

    If String.IsNullOrEmpty(filename) Then
        MsgBox("No file selected")
        Return
    End If

    Process.Start("EXCEL.EXE", filename)
End Sub

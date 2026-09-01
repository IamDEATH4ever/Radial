$dllPath = 'C:\Users\gaura\.nuget\packages\skiasharp\4.151.1\lib\net10.0-windows10.0.19041\SkiaSharp.dll'
Add-Type -Path $dllPath
[SkiaSharp.SKRuntimeEffect].GetMethods() | Where-Object { $_.Name -like '*Create*' -or $_.Name -eq 'ToShader' } | Select-Object Name, ReturnType, @{Name='Parameters';Expression={($_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }) -join ', '}} | Format-List

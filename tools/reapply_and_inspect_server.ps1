$serverPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py"
$templatePath = "C:\Users\user\Documents\GitHub\ProjectCRT\tools\server_updated.py"

Copy-Item -LiteralPath $templatePath -Destination $serverPath -Force

Write-Output "--- VERIFY TOKENS ---"
Select-String -Path $serverPath -Pattern "_derive_interrogation_state|interrogationState|lastKnownAffect|lastKnownPatience|DEFAULT_SIGNAL|_derive_signal|stress|distortion|focus" | ForEach-Object {
    Write-Output ($_.LineNumber.ToString() + ':' + $_.Line.Trim())
}

Write-Output "--- HARDCODE KEYWORDS ---"
Select-String -Path $serverPath -Pattern "노이즈|보청기|CRT|브라운관|잡음|화이트" | ForEach-Object {
    Write-Output ($_.LineNumber.ToString() + ':' + $_.Line.Trim())
}

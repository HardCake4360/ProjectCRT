$serverPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py"
$lines = Get-Content -LiteralPath $serverPath
$output = New-Object System.Collections.Generic.List[string]
$skipCount = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($skipCount -gt 0) {
        $skipCount--
        continue
    }

    $line = $lines[$i]

    if ($line -like 'PDF_NAME = *') {
        $output.Add('PDF_NAME = "DatabasePrompt"  # PDF파일 이름')
        $output.Add('BASE_DIR = os.path.dirname(os.path.abspath(__file__))')
        $output.Add('DATA_DIR = os.path.join(BASE_DIR, "data")')
        $output.Add('PDF_PATH = os.path.join(DATA_DIR, "pdfs", PDF_NAME + ".pdf")  # PDF 경로')
        $output.Add('INDEX_DIR = os.path.join(DATA_DIR, "index")')
        $output.Add('INDEX_PATH = os.path.join(INDEX_DIR, "world")')
        $output.Add('WORLD_DIR = os.path.join(DATA_DIR, "world")')
        $skipCount = 3
        continue
    }

    if ($line -eq 'retriever = Retriever()') {
        $output.Add('retriever = Retriever()')
        $output.Add('os.makedirs(INDEX_DIR, exist_ok=True)')

        while (($i + 1) -lt $lines.Count -and $lines[$i + 1] -like 'os.makedirs(INDEX_DIR*') {
            $i++
        }
        continue
    }

    if ($line -like 'os.makedirs(INDEX_DIR*') {
        continue
    }

    $output.Add($line)
}

$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($serverPath, $output, $encoding)
Write-Output "server.py path constants normalized"

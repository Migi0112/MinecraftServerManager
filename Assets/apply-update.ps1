$ErrorActionPreference='Stop'
# Never resolve Framework UI references from the app's bundled .NET 8 DLL directory.
Set-Location -LiteralPath $PSHOME
[Environment]::CurrentDirectory=$PSHOME
trap {
    @{success=$false;error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath ($PSCommandPath+'.result.json') -Encoding utf8
    exit 1
}
function Get-InstallerHash {
    $taskStream=[IO.File]::OpenRead($taskInstaller)
    $taskSha=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskSha.ComputeHash($taskStream)).Replace('-','') }
    finally { $taskStream.Dispose(); $taskSha.Dispose() }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$taskScript=$PSCommandPath
$taskReady=$taskScript+'.ready'
$taskResult=$taskScript+'.result.json'
Add-Type -ReferencedAssemblies System.Windows.Forms,System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
public sealed class ManagerInstallWindow : Form {
 public readonly Label StatusLabel=new Label();
 [System.Runtime.InteropServices.DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int AddFontResourceEx(string path,uint flags,IntPtr reserved);
 [System.Runtime.InteropServices.DllImport("gdi32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern bool RemoveFontResourceEx(string path,uint flags,IntPtr reserved);
 string loadedFont; readonly Timer animation=new Timer(); int offset;
 static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public ManagerInstallWindow(string directory){
  if(System.IO.File.Exists(directory)){loadedFont=directory;AddFontResourceEx(directory,16,IntPtr.Zero);}
  Text="마인크래프트 서버 매니저 · 업데이트";ClientSize=new Size(660,380);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.None;ControlBox=false;DoubleBuffered=true;BackColor=Color.FromArgb(250,247,242);Font=new Font("Noto Sans KR",11);
  using(var path=Round(new Rectangle(0,0,660,380),22))Region=new Region(path);
  StatusLabel.Text="설치가 끝나면 메인 화면으로 자동으로 시작합니다.";StatusLabel.ForeColor=Color.FromArgb(108,111,114);StatusLabel.BackColor=Color.White;StatusLabel.SetBounds(52,174,556,50);Controls.Add(StatusLabel);
  animation.Interval=30;animation.Tick+=delegate{offset=(offset+5)%660;Invalidate(new Rectangle(52,244,556,16));};animation.Start();
 }
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color coral=Color.FromArgb(255,117,91),ink=Color.FromArgb(65,43,32),line=Color.FromArgb(231,221,210);using(var card=Round(new Rectangle(24,88,612,222),18))using(var pen=new Pen(line)){g.FillPath(Brushes.White,card);g.DrawPath(pen,card);}using(var titleFont=new Font("Noto Sans KR",17,FontStyle.Bold))using(var font=new Font("Noto Sans KR",11))using(var titleBrush=new SolidBrush(ink))using(var accent=new SolidBrush(coral)){
  g.FillEllipse(accent,26,24,42,42);using(var whitePen=new Pen(Color.White,3)){g.DrawLine(whitePen,47,55,47,34);g.DrawLine(whitePen,47,34,39,42);g.DrawLine(whitePen,47,34,55,42);}g.DrawString("프로그램 업데이트",titleFont,titleBrush,82,28);
  g.DrawString("새 버전을 설치하고 있어요",titleFont,titleBrush,48,120);
  g.DrawString("다운로드 완료   ›   파일 확인 완료   ›   설치 중",font,accent,50,274);
  using(var track=Round(new Rectangle(52,244,556,12),6))using(var trackBrush=new SolidBrush(Color.FromArgb(248,232,222))){g.FillPath(trackBrush,track);var state=g.Save();g.SetClip(track);g.FillRectangle(accent,52+offset-104,244,104,12);g.Restore(state);}
  g.DrawString("잠시만 기다려 주세요",font,titleBrush,26,335);using(var small=new Font("Noto Sans KR",9))g.DrawString("제작자 Migi0112",small,titleBrush,530,340);
 }}
 public static Form Failure(string message){
  var f=new Form();f.Text="업데이트 확인";f.AutoScaleMode=AutoScaleMode.None;f.ClientSize=new Size(660,410);f.FormBorderStyle=FormBorderStyle.None;f.StartPosition=FormStartPosition.CenterScreen;f.BackColor=Color.FromArgb(250,247,242);f.Font=new Font("Noto Sans KR",11);
  using(var path=Round(new Rectangle(0,0,660,410),22))f.Region=new Region(path);
  var title=new Label();title.Text="설치를 완료하지 못했어요";title.Font=new Font("Noto Sans KR",20,FontStyle.Bold);title.ForeColor=Color.FromArgb(65,43,32);title.SetBounds(30,26,600,62);f.Controls.Add(title);
  var reason=new TextBox();reason.Multiline=true;reason.ReadOnly=true;reason.ScrollBars=ScrollBars.Vertical;reason.BorderStyle=BorderStyle.None;reason.BackColor=Color.White;reason.ForeColor=Color.FromArgb(65,43,32);reason.Text=message;reason.SetBounds(32,110,596,128);f.Controls.Add(reason);
  var note=new Label();note.Text="설치 파일은 다시 시도할 수 있도록 보관됩니다.";note.ForeColor=Color.FromArgb(108,111,114);note.SetBounds(32,256,596,50);f.Controls.Add(note);
  var ok=new Button();ok.Text="확인";ok.BackColor=Color.FromArgb(255,117,91);ok.ForeColor=Color.White;ok.FlatStyle=FlatStyle.Flat;ok.FlatAppearance.BorderSize=0;ok.Cursor=Cursors.Hand;ok.SetBounds(360,326,268,56);using(var path=Round(new Rectangle(0,0,268,56),14))ok.Region=new Region(path);ok.DialogResult=DialogResult.OK;f.Controls.Add(ok);f.AcceptButton=ok;f.CancelButton=ok;return f;
 }
 protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);if(disposing&&loadedFont!=null)RemoveFontResourceEx(loadedFont,16,IntPtr.Zero);}
}
"@
$taskFont=$taskScript+'.ttf'
$taskFontSource=Join-Path $taskDirectory 'Assets/Fonts/NotoSansKR-VF.ttf'
if(Test-Path -LiteralPath $taskFontSource){Copy-Item -LiteralPath $taskFontSource -Destination $taskFont}
$taskForm=New-Object ManagerInstallWindow -ArgumentList $taskFont
$taskStatus=$taskForm.StatusLabel
try {
    if((Get-InstallerHash) -ne $taskHash){throw '설치 파일 검증에 실패했습니다.'}
    [IO.File]::WriteAllText($taskReady,'ready')
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(90)
    while(Get-Process -Id $taskParent -ErrorAction SilentlyContinue){
        if([DateTime]::UtcNow -gt $taskDeadline){throw '프로그램 종료를 기다리다가 시간이 초과됐습니다. 설치하지 않았습니다.'}
        Start-Sleep -Milliseconds 200
    }
    $taskForm.Show()
    [Windows.Forms.Application]::DoEvents()
    # Recheck after the parent exits; do not execute changed cached bytes.
    if((Get-InstallerHash) -ne $taskHash){throw '설치 파일이 변경되어 설치를 중단했습니다.'}
    $taskSetup=Start-Process -FilePath $taskInstaller -ArgumentList @('/SP-','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$taskDirectory+'"')) -WindowStyle Hidden -PassThru
    while(!$taskSetup.HasExited){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 100;$taskSetup.Refresh()}
    if($taskSetup.ExitCode -ne 0){throw ('설치가 완료되지 않았습니다. 오류 코드: '+$taskSetup.ExitCode)}
    $taskStatus.Text='설치 완료 · 임시 파일 정리 후 메인 화면을 여는 중…'
    [Windows.Forms.Application]::DoEvents()
    Remove-Item -LiteralPath $taskInstaller -Force
    $taskExe=Join-Path $taskDirectory 'NeoForgeServerManager.exe'
    if(!(Test-Path -LiteralPath $taskExe)){throw '설치된 프로그램을 찾을 수 없습니다.'}
    $taskApp=Start-Process -FilePath $taskExe -WorkingDirectory $taskDirectory -WindowStyle Normal -PassThru
    @{success=$true;pid=$taskApp.Id} | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding utf8
} catch {
    @{success=$false;error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath $taskResult -Encoding utf8
    $taskFailure=[ManagerInstallWindow]::Failure($_.Exception.Message)
    try {$taskFailure.ShowDialog($taskForm) | Out-Null} finally {$taskFailure.Dispose()}
} finally {
    $taskForm.Dispose()
    Remove-Item -LiteralPath $taskFont -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $taskReady -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $taskScript -Force -ErrorAction SilentlyContinue
}

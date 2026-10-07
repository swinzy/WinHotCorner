// Shared by WinHotCorner.iss and ControlPanel.iss, inside [Code]: a framed note with a link under the uiaccess task
// on the Select Additional Tasks page

procedure UIAccessNoteLinkClick(Sender: TObject; const Link: String; LinkType: TSysLinkType);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', Link, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure AddUIAccessNote();
var
  Frame: TBevel;
  Note: TNewLinkLabel;
  Page: TWizardPage;
  Url: String;
begin
  Page := PageFromID(wpSelectTasks);
  Url := 'https://github.com/swinzy/WinHotCorner/blob/main/docs/digital-signature.md';

  Note := TNewLinkLabel.Create(Page);
  Note.Parent := Page.Surface;
  Note.AutoSize := False;
  Note.Left := WizardForm.TasksList.Left + ScaleX(12);
  Note.Width := WizardForm.TasksList.Width - ScaleX(24);
  Note.Height := ScaleY(80);
  Note.Caption := 'This will enable WinHotCorner to run without administrator rights and show its ripple effect ' +
    'normally. A one-time digital certificate will be used to sign this copy of WinHotCorner.' + #13#10#13#10 +
    'For more information, please visit: <a href="' + Url + '">' + Url + '</a>';
  Note.OnLinkClick := @UIAccessNoteLinkClick;

  // The task list keeps the room its one task needs, the note goes below it
  WizardForm.TasksList.Height := ScaleY(32);
  Note.Top := WizardForm.TasksList.Top + WizardForm.TasksList.Height + ScaleY(20);

  Frame := TBevel.Create(Page);
  Frame.Parent := Page.Surface;
  Frame.Shape := bsFrame;
  Frame.Left := WizardForm.TasksList.Left;
  Frame.Width := WizardForm.TasksList.Width;
  Frame.Top := Note.Top - ScaleY(10);
  Frame.Height := Note.Height + ScaleY(20);
end;

// Shared by WinHotCorner.iss and ControlPanel.iss, inside [Code]: the choice between the standard (signed) install
// and installing without signing on the Select Additional Tasks page (renamed in Languages.iss), and a framed note below it that changes with
// the choice: a short one for the standard install, a warning for installing without signing, each with a link

const
  SignUrl = 'https://github.com/swinzy/WinHotCorner/blob/main/docs/digital-signature.md';
  UnsignedUrl = 'https://github.com/swinzy/WinHotCorner/blob/main/docs/digital-signature.md#installing-without-signing';

var
  UIAccessNote: TNewLinkLabel;
  UIAccessWarning: TBitmapImage;
  UIAccessNoteLeft, UIAccessNoteWidth: Integer;

procedure UIAccessNoteLinkClick(Sender: TObject; const Link: String; LinkType: TSysLinkType);
var
  ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', Link, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

function NoteLink(const Url, Text: String): String;
begin
  Result := '<a href="' + Url + '">' + Text + '</a>';
end;

function SignNoteText(const Link: String): String;
begin
  Result := FmtMessage(CustomMessage('SignNote'), [Link]);
end;

function UnsignedNoteText(const Link: String): String;
begin
  Result := FmtMessage(CustomMessage('UnsignedNote'), [Link]);
end;

// The height a label of this width and font needs for the text
function MeasureNote(const Page: TWizardPage; const Text: String; const Width: Integer): Integer;
var
  Measure: TNewStaticText;
begin
  Measure := TNewStaticText.Create(Page);
  Measure.Parent := Page.Surface;
  Measure.Visible := False;
  Measure.AutoSize := False;
  Measure.WordWrap := True;
  Measure.Width := Width;
  Measure.Caption := Text;
  Measure.AdjustHeight();
  Result := Measure.Height;
  Measure.Free();
end;

// Tasks given on the command line
function TasksOnCommandLine(): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if (Pos('/TASKS=', Uppercase(ParamStr(I))) = 1) or (Pos('/MERGETASKS=', Uppercase(ParamStr(I))) = 1) then
      Result := True;
end;

// One of the two must be chosen; if neither is, that means without signing
procedure EnsureInstallChoice();
begin
  if not WizardIsTaskSelected('uiaccess') and not WizardIsTaskSelected('unsigned') then
    WizardSelectTasks('unsigned');
end;

procedure UpdateUIAccessNote();
var
  Unsigned: Boolean;
  Indent: Integer;
begin
  Unsigned := not WizardIsTaskSelected('uiaccess');
  UIAccessWarning.Visible := Unsigned;
  if Unsigned then
  begin
    Indent := UIAccessWarning.Width + ScaleX(10);
    UIAccessNote.Caption := UnsignedNoteText(NoteLink(UnsignedUrl, CustomMessage('UnsignedLink')));
  end
  else
  begin
    Indent := 0;
    UIAccessNote.Caption := SignNoteText(NoteLink(SignUrl, CustomMessage('SignLink')));
  end;
  UIAccessNote.Left := UIAccessNoteLeft + Indent;
  UIAccessNote.Width := UIAccessNoteWidth - Indent;
end;

procedure TasksListClickCheck(Sender: TObject);
begin
  UpdateUIAccessNote();
end;

// Call when the Select Additional Tasks page is shown
procedure UIAccessPageShown();
begin
  EnsureInstallChoice();
  UpdateUIAccessNote();
end;

procedure AddUIAccessNote();
var
  Frame: TBevel;
  Page: TWizardPage;
  Height, WarningHeight: Integer;
begin
  Page := PageFromID(wpSelectTasks);
  UIAccessNoteLeft := WizardForm.TasksList.Left + ScaleX(12);
  UIAccessNoteWidth := WizardForm.TasksList.Width - ScaleX(24);

  UIAccessWarning := TBitmapImage.Create(Page);
  UIAccessWarning.Parent := Page.Surface;
  UIAccessWarning.Width := ScaleX(24);
  UIAccessWarning.Height := ScaleY(24);
  InitializeBitmapImageFromStockIcon(UIAccessWarning, SIID_WARNING, Page.Surface.Color, []);

  UIAccessNote := TNewLinkLabel.Create(Page);
  UIAccessNote.Parent := Page.Surface;
  UIAccessNote.AutoSize := False;
  UIAccessNote.OnLinkClick := @UIAccessNoteLinkClick;

  // As tall as the longer note needs in this language, so the page does not move when the choice changes
  WarningHeight := MeasureNote(Page, UnsignedNoteText(CustomMessage('UnsignedLink')), UIAccessNoteWidth - UIAccessWarning.Width - ScaleX(10));
  if WarningHeight < UIAccessWarning.Height then
    WarningHeight := UIAccessWarning.Height;
  Height := MeasureNote(Page, SignNoteText(CustomMessage('SignLink')), UIAccessNoteWidth);
  if Height < WarningHeight then
    Height := WarningHeight;
  UIAccessNote.Height := Height;

  // The task list keeps the room its two choices need, the note goes below it
  WizardForm.TasksList.Height := ScaleY(46);
  UIAccessNote.Top := WizardForm.TasksList.Top + WizardForm.TasksList.Height + ScaleY(20);
  UIAccessWarning.Left := UIAccessNoteLeft;
  UIAccessWarning.Top := UIAccessNote.Top;

  Frame := TBevel.Create(Page);
  Frame.Parent := Page.Surface;
  Frame.Shape := bsFrame;
  Frame.Left := WizardForm.TasksList.Left;
  Frame.Width := WizardForm.TasksList.Width;
  Frame.Top := UIAccessNote.Top - ScaleY(10);
  Frame.Height := UIAccessNote.Height + ScaleY(20);

  WizardForm.TasksList.OnClickCheck := @TasksListClickCheck;
end;

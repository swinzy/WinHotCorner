; Shared by WinHotCorner.iss and ControlPanel.iss: the languages of the installers, and their own text.
; Inno Setup's text comes with it (compiler:Languages); Setup picks the language of Windows' display language.
; English is written in Australian English, the project's spelling.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Without an exact match Setup takes the first language with the same primary language: Traditional Chinese for Hong Kong
; and Macao, Latin American Spanish for every Spanish but Spain's
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "spanishlatinamerica"; MessagesFile: "compiler:Languages\Spanish.isl,SpanishLatinAmerica.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.SignTask=Sign WinHotCorner on this computer
english.SignNote=This will enable WinHotCorner to run without administrator rights and show its ripple effect normally. A one-time digital certificate will be used to sign this copy of WinHotCorner.
english.SignNoteMore=For more information, please visit: %1
english.SignFailedBefore=Could not sign %1 on this computer, so it will be installed the usual way instead: it runs with administrator rights, and its ripple shows under Task View.
english.SignFailedAfter=Could not sign %1 on this computer, so it was installed the usual way instead: it runs with administrator rights, and its ripple shows under Task View.
english.CertRemoveFailed=Could not remove the certificate that %1 made on this computer before. It is named "WinHotCorner (made on this computer)" in Trusted Root Certification Authorities.
english.NeedNetFx=%1 needs .NET Framework 4.8 or later, which comes with Windows 10 version 1903 and later.
english.TaskWriteFailed=Could not write the startup task definition.
english.TaskRegisterFailed=Could not register the startup task. %1 will not start at sign-in.
english.HotCornerInstallFailed=Could not install WinHotCorner (exit code %1).
english.OpenControlPanel=Open %1

chinesesimplified.SignTask=在这台电脑上为 WinHotCorner 签名
chinesesimplified.SignNote=这样 WinHotCorner 无需管理员权限即可运行，并能正常显示波纹效果。将使用一张一次性的数字证书为这份 WinHotCorner 签名。
chinesesimplified.SignNoteMore=详细信息请访问：%1
chinesesimplified.SignFailedBefore=无法在这台电脑上为 %1 签名，因此将改用常规方式安装：它会以管理员权限运行，波纹会显示在任务视图下方。
chinesesimplified.SignFailedAfter=无法在这台电脑上为 %1 签名，因此已改用常规方式安装：它会以管理员权限运行，波纹会显示在任务视图下方。
chinesesimplified.CertRemoveFailed=无法删除 %1 之前在这台电脑上生成的证书。它位于“受信任的根证书颁发机构”中，名为“WinHotCorner (made on this computer)”。
chinesesimplified.NeedNetFx=%1 需要 .NET Framework 4.8 或更高版本，Windows 10 1903 及更高版本已自带。
chinesesimplified.TaskWriteFailed=无法写入启动任务的定义。
chinesesimplified.TaskRegisterFailed=无法注册启动任务，%1 将不会在登录时启动。
chinesesimplified.HotCornerInstallFailed=无法安装 WinHotCorner（退出代码 %1）。
chinesesimplified.OpenControlPanel=打开 %1

chinesetraditional.SignTask=在這台電腦上為 WinHotCorner 簽署
chinesetraditional.SignNote=這樣 WinHotCorner 不需要系統管理員權限即可執行，並能正常顯示漣漪效果。將使用一張一次性的數位憑證為這份 WinHotCorner 簽署。
chinesetraditional.SignNoteMore=詳細資訊請參閱：%1
chinesetraditional.SignFailedBefore=無法在這台電腦上為 %1 簽署，因此將改用一般方式安裝：它會以系統管理員權限執行，漣漪會顯示在工作檢視下方。
chinesetraditional.SignFailedAfter=無法在這台電腦上為 %1 簽署，因此已改用一般方式安裝：它會以系統管理員權限執行，漣漪會顯示在工作檢視下方。
chinesetraditional.CertRemoveFailed=無法移除 %1 先前在這台電腦上產生的憑證。它位於「受信任的根憑證授權單位」中，名稱為「WinHotCorner (made on this computer)」。
chinesetraditional.NeedNetFx=%1 需要 .NET Framework 4.8 或更新版本，Windows 10 1903 及更新版本已內建。
chinesetraditional.TaskWriteFailed=無法寫入啟動工作的定義。
chinesetraditional.TaskRegisterFailed=無法登錄啟動工作，%1 將不會在登入時啟動。
chinesetraditional.HotCornerInstallFailed=無法安裝 WinHotCorner（結束代碼 %1）。
chinesetraditional.OpenControlPanel=開啟 %1

spanishlatinamerica.SignTask=Firmar WinHotCorner en este equipo
spanishlatinamerica.SignNote=Esto permitirá que WinHotCorner se ejecute sin derechos de administrador y muestre su efecto de onda con normalidad. Se usará un certificado digital de un solo uso para firmar esta copia de WinHotCorner.
spanishlatinamerica.SignNoteMore=Para obtener más información, visita: %1
spanishlatinamerica.SignFailedBefore=No se pudo firmar %1 en este equipo, así que se instalará de la forma habitual: se ejecuta con derechos de administrador y su efecto de onda aparece debajo de la Vista de tareas.
spanishlatinamerica.SignFailedAfter=No se pudo firmar %1 en este equipo, así que se instaló de la forma habitual: se ejecuta con derechos de administrador y su efecto de onda aparece debajo de la Vista de tareas.
spanishlatinamerica.CertRemoveFailed=No se pudo quitar el certificado que %1 creó antes en este equipo. Se llama "WinHotCorner (made on this computer)" y está en Entidades de certificación raíz de confianza.
spanishlatinamerica.NeedNetFx=%1 necesita .NET Framework 4.8 o posterior, que viene con Windows 10, versión 1903 y posteriores.
spanishlatinamerica.TaskWriteFailed=No se pudo escribir la definición de la tarea de inicio.
spanishlatinamerica.TaskRegisterFailed=No se pudo registrar la tarea de inicio. %1 no se iniciará al iniciar sesión.
spanishlatinamerica.HotCornerInstallFailed=No se pudo instalar WinHotCorner (código de salida %1).
spanishlatinamerica.OpenControlPanel=Abrir %1

spanish.SignTask=Firmar WinHotCorner en este equipo
spanish.SignNote=Esto permitirá que WinHotCorner se ejecute sin derechos de administrador y muestre su efecto de onda con normalidad. Se usará un certificado digital de un solo uso para firmar esta copia de WinHotCorner.
spanish.SignNoteMore=Para obtener más información, visita: %1
spanish.SignFailedBefore=No se ha podido firmar %1 en este equipo, así que se instalará de la forma habitual: se ejecuta con derechos de administrador y su efecto de onda aparece debajo de la Vista de tareas.
spanish.SignFailedAfter=No se ha podido firmar %1 en este equipo, así que se ha instalado de la forma habitual: se ejecuta con derechos de administrador y su efecto de onda aparece debajo de la Vista de tareas.
spanish.CertRemoveFailed=No se ha podido quitar el certificado que %1 creó antes en este equipo. Se llama "WinHotCorner (made on this computer)" y está en Entidades de certificación raíz de confianza.
spanish.NeedNetFx=%1 necesita .NET Framework 4.8 o posterior, que viene con Windows 10, versión 1903 y posteriores.
spanish.TaskWriteFailed=No se ha podido escribir la definición de la tarea de inicio.
spanish.TaskRegisterFailed=No se ha podido registrar la tarea de inicio. %1 no se iniciará al iniciar sesión.
spanish.HotCornerInstallFailed=No se ha podido instalar WinHotCorner (código de salida %1).
spanish.OpenControlPanel=Abrir %1

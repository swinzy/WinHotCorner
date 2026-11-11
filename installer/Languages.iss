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

[Messages]
; The Select Additional Tasks page only holds the choice between the standard install and installing without signing
english.WizardSelectTasks=Choose Installation Type
english.SelectTasksDesc=How should WinHotCorner be installed?
english.SelectTasksLabel2=Choose how to install WinHotCorner, then click Next.
chinesesimplified.WizardSelectTasks=选择安装类型
chinesesimplified.SelectTasksDesc=要如何安装 WinHotCorner？
chinesesimplified.SelectTasksLabel2=请选择 WinHotCorner 的安装方式，然后点击“下一步”。
chinesetraditional.WizardSelectTasks=選擇安裝類型
chinesetraditional.SelectTasksDesc=要如何安裝 WinHotCorner？
chinesetraditional.SelectTasksLabel2=請選擇 WinHotCorner 的安裝方式，然後按「下一步」。
spanishlatinamerica.WizardSelectTasks=Seleccione el Tipo de Instalación
spanishlatinamerica.SelectTasksDesc=¿Cómo se debe instalar WinHotCorner?
spanishlatinamerica.SelectTasksLabel2=Seleccione cómo instalar WinHotCorner y haga clic en Siguiente.
spanish.WizardSelectTasks=Seleccione el Tipo de Instalación
spanish.SelectTasksDesc=¿Cómo se debe instalar WinHotCorner?
spanish.SelectTasksLabel2=Seleccione cómo instalar WinHotCorner y haga clic en Siguiente.

[CustomMessages]
english.SignTask=Standard install (recommended)
english.UnsignedTask=Install without signing (limited)
english.SignNote=A one-time local digital certificate will be used to sign this copy of WinHotCorner.%n%nTo learn how this signature works, see: %1.
english.SignLink=Signing WinHotCorner on your computer
english.UnsignedNote=The standard installation enables Windows uiAccess for reliable hot corner activation.%nWithout signing, WinHotCorner is subject to Windows security restrictions that require compatibility workarounds.%n%nUnsigned installations receive best-effort support only. For details, see: %1.
english.UnsignedLink=Installing without signing
english.SignFailedBefore=Could not sign %1 on this computer, so it will be installed without signing instead: it has known limitations, and gets best-effort support only.
english.SignFailedAfter=Could not sign %1 on this computer, so it was installed without signing instead: it has known limitations, and gets best-effort support only.
english.CertRemoveFailed=Could not remove the certificate that %1 made on this computer before. It is named "WinHotCorner (made on this computer)" in Trusted Root Certification Authorities.
english.NeedNetFx=%1 needs .NET Framework 4.8 or later, which comes with Windows 10 version 1903 and later.
english.TaskWriteFailed=Could not write the startup task definition.
english.TaskRegisterFailed=Could not register the startup task. %1 will not start at sign-in.
english.HotCornerInstallFailed=Could not install WinHotCorner (exit code %1).
english.OpenControlPanel=Open %1

chinesesimplified.SignTask=标准安装（推荐）
chinesesimplified.UnsignedTask=不签名安装（受限）
chinesesimplified.SignNote=将使用一张一次性的本地数字证书为这份 WinHotCorner 签名。%n%n想了解这份签名如何运作，请参见：%1。
chinesesimplified.SignLink=在电脑上为 WinHotCorner 签名
chinesesimplified.UnsignedNote=标准安装会启用 Windows 的 uiAccess，让热角稳定触发。%n不签名时，WinHotCorner 受 Windows 安全限制，需要采用兼容性变通方法。%n%n不签名安装仅提供尽力支持。详情请参阅：%1。
chinesesimplified.UnsignedLink=不签名安装
chinesesimplified.SignFailedBefore=无法在这台电脑上为 %1 签名，因此将改为不签名安装：它存在已知限制，且仅提供尽力支持。
chinesesimplified.SignFailedAfter=无法在这台电脑上为 %1 签名，因此已改为不签名安装：它存在已知限制，且仅提供尽力支持。
chinesesimplified.CertRemoveFailed=无法删除 %1 之前在这台电脑上生成的证书。它位于“受信任的根证书颁发机构”中，名为“WinHotCorner (made on this computer)”。
chinesesimplified.NeedNetFx=%1 需要 .NET Framework 4.8 或更高版本，Windows 10 1903 及更高版本已自带。
chinesesimplified.TaskWriteFailed=无法写入启动任务的定义。
chinesesimplified.TaskRegisterFailed=无法注册启动任务，%1 将不会在登录时启动。
chinesesimplified.HotCornerInstallFailed=无法安装 WinHotCorner（退出代码 %1）。
chinesesimplified.OpenControlPanel=打开 %1

chinesetraditional.SignTask=標準安裝（建議）
chinesetraditional.UnsignedTask=不簽署安裝（受限）
chinesetraditional.SignNote=將使用一張一次性的本機數位憑證為這份 WinHotCorner 簽署。%n%n想了解這份簽章如何運作，請參閱：%1。
chinesetraditional.SignLink=在電腦上為 WinHotCorner 簽署
chinesetraditional.UnsignedNote=標準安裝會啟用 Windows 的 uiAccess，讓熱角穩定觸發。%n不簽署時，WinHotCorner 會受到 Windows 安全性限制，需要採用相容性因應措施。%n%n不簽署的安裝僅提供盡力支援。詳細資訊請參閱：%1。
chinesetraditional.UnsignedLink=不簽署安裝
chinesetraditional.SignFailedBefore=無法在這台電腦上為 %1 簽署，因此將改為不簽署安裝：它有已知限制，且僅提供盡力支援。
chinesetraditional.SignFailedAfter=無法在這台電腦上為 %1 簽署，因此已改為不簽署安裝：它有已知限制，且僅提供盡力支援。
chinesetraditional.CertRemoveFailed=無法移除 %1 先前在這台電腦上產生的憑證。它位於「受信任的根憑證授權單位」中，名稱為「WinHotCorner (made on this computer)」。
chinesetraditional.NeedNetFx=%1 需要 .NET Framework 4.8 或更新版本，Windows 10 1903 及更新版本已內建。
chinesetraditional.TaskWriteFailed=無法寫入啟動工作的定義。
chinesetraditional.TaskRegisterFailed=無法登錄啟動工作，%1 將不會在登入時啟動。
chinesetraditional.HotCornerInstallFailed=無法安裝 WinHotCorner（結束代碼 %1）。
chinesetraditional.OpenControlPanel=開啟 %1

spanishlatinamerica.SignTask=Instalación estándar (recomendado)
spanishlatinamerica.UnsignedTask=Instalar sin firmar (limitado)
spanishlatinamerica.SignNote=Se usará un certificado digital local de un solo uso para firmar esta copia de WinHotCorner.%n%nPara saber cómo funciona esta firma, consulta: %1.
spanishlatinamerica.SignLink=Firmar WinHotCorner en tu equipo
spanishlatinamerica.UnsignedNote=La instalación estándar habilita uiAccess de Windows para que la esquina activa funcione de forma fiable.%nSin firmar, WinHotCorner está sujeto a restricciones de seguridad de Windows que requieren soluciones de compatibilidad.%n%nLas instalaciones sin firmar solo reciben soporte en la medida de lo posible. Para obtener más información, consulta: %1.
spanishlatinamerica.UnsignedLink=Instalar sin firmar
spanishlatinamerica.SignFailedBefore=No se pudo firmar %1 en este equipo, así que se instalará sin firmar: tiene limitaciones conocidas y solo recibe soporte en la medida de lo posible.
spanishlatinamerica.SignFailedAfter=No se pudo firmar %1 en este equipo, así que se instaló sin firmar: tiene limitaciones conocidas y solo recibe soporte en la medida de lo posible.
spanishlatinamerica.CertRemoveFailed=No se pudo quitar el certificado que %1 creó antes en este equipo. Se llama "WinHotCorner (made on this computer)" y está en Entidades de certificación raíz de confianza.
spanishlatinamerica.NeedNetFx=%1 necesita .NET Framework 4.8 o posterior, que viene con Windows 10, versión 1903 y posteriores.
spanishlatinamerica.TaskWriteFailed=No se pudo escribir la definición de la tarea de inicio.
spanishlatinamerica.TaskRegisterFailed=No se pudo registrar la tarea de inicio. %1 no se iniciará al iniciar sesión.
spanishlatinamerica.HotCornerInstallFailed=No se pudo instalar WinHotCorner (código de salida %1).
spanishlatinamerica.OpenControlPanel=Abrir %1

spanish.SignTask=Instalación estándar (recomendado)
spanish.UnsignedTask=Instalar sin firmar (limitado)
spanish.SignNote=Se usará un certificado digital local de un solo uso para firmar esta copia de WinHotCorner.%n%nPara saber cómo funciona esta firma, consulta: %1.
spanish.SignLink=Firmar WinHotCorner en tu equipo
spanish.UnsignedNote=La instalación estándar habilita uiAccess de Windows para que la esquina activa funcione de forma fiable.%nSin firmar, WinHotCorner está sujeto a restricciones de seguridad de Windows que requieren soluciones de compatibilidad.%n%nLas instalaciones sin firmar solo reciben soporte en la medida de lo posible. Para obtener más información, consulta: %1.
spanish.UnsignedLink=Instalar sin firmar
spanish.SignFailedBefore=No se ha podido firmar %1 en este equipo, así que se instalará sin firmar: tiene limitaciones conocidas y solo recibe soporte en la medida de lo posible.
spanish.SignFailedAfter=No se ha podido firmar %1 en este equipo, así que se ha instalado sin firmar: tiene limitaciones conocidas y solo recibe soporte en la medida de lo posible.
spanish.CertRemoveFailed=No se ha podido quitar el certificado que %1 creó antes en este equipo. Se llama "WinHotCorner (made on this computer)" y está en Entidades de certificación raíz de confianza.
spanish.NeedNetFx=%1 necesita .NET Framework 4.8 o posterior, que viene con Windows 10, versión 1903 y posteriores.
spanish.TaskWriteFailed=No se ha podido escribir la definición de la tarea de inicio.
spanish.TaskRegisterFailed=No se ha podido registrar la tarea de inicio. %1 no se iniciará al iniciar sesión.
spanish.HotCornerInstallFailed=No se ha podido instalar WinHotCorner (código de salida %1).
spanish.OpenControlPanel=Abrir %1

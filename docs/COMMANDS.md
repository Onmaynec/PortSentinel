# ⌨️ CLI-команды PortSentinel

> Это целевой CLI-контракт. Команды будут добавляться поэтапно и пока не гарантируются исполняемой версией.

## Общие

```powershell
portsentinel --help
portsentinel --version
portsentinel doctor
portsentinel status
```

## Live-мониторинг

```powershell
portsentinel live
portsentinel live --duration 2m
portsentinel live --profile standard
portsentinel live --filter process=Discord.exe
portsentinel live --filter remote-port=443
portsentinel live --only-new
```

## Connections и listeners

```powershell
portsentinel connections
portsentinel connections --protocol tcp
portsentinel connections --state established
portsentinel connections --process steam.exe
portsentinel connections --remote-port 443
portsentinel connections --format json

portsentinel listeners
portsentinel listeners --protocol tcp
portsentinel listeners --address 0.0.0.0
portsentinel listeners --unsigned-only
portsentinel listeners --new-since normal-workstation
```

## Процессы и приложения

```powershell
portsentinel process show <pid>
portsentinel process network <pid>
portsentinel process history <pid-or-name>
portsentinel process verify <path>

portsentinel watch process Discord.exe
portsentinel watch pid 8420
portsentinel launch .\Application.exe --arg "--test"
portsentinel watch installer .\Setup.exe --post-exit 60s
```

## Сессии

```powershell
portsentinel session list
portsentinel session show <id>
portsentinel session rename <id> <name>
portsentinel session delete <id>
portsentinel session cleanup --older-than 30d
```

## Baseline

```powershell
portsentinel baseline create <name> --duration 10m
portsentinel baseline list
portsentinel baseline show <name>
portsentinel baseline compare <name>
portsentinel baseline delete <name>
```

## Quickscan

```powershell
portsentinel quickscan
```

Проверки должны включать TCP listeners, UDP endpoints, unsigned network processes, wildcard listeners, процессы из Temp/Downloads, invalid signatures, baseline deviations и inbound allow rules.

## Firewall

```powershell
portsentinel firewall status
portsentinel firewall rules
portsentinel firewall plan block-process C:\Apps\Unknown.exe
portsentinel firewall block-process C:\Apps\Unknown.exe --dry-run
portsentinel firewall block-process C:\Apps\Unknown.exe
portsentinel firewall allow-process C:\Apps\Trusted.exe
portsentinel firewall block-endpoint 203.0.113.0/24
portsentinel firewall remove <rule-id>
portsentinel firewall rollback <transaction-id>
```

Любое изменение должно показывать план, поддерживать dry-run, требовать подтверждение и затрагивать только правила группы `PortSentinel`.

## Reports

```powershell
portsentinel report <session-id> --format console
portsentinel report <session-id> --format html
portsentinel report <session-id> --format json
portsentinel report <session-id> --format markdown
```

## Non-interactive mode

```powershell
portsentinel live --duration 60s --non-interactive --format json
```

В этом режиме нельзя задавать вопросы или показывать анимации; exit codes и JSON schema должны быть стабильными.

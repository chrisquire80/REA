# Firma Authenticode

Gli eseguibili della suite escono **non firmati** finché non configuri un certificato. Il workflow di release e `crea_pacchetto_portatile.ps1` chiamano `firma_authenticode.ps1`. Se nessuna delle variabili sotto è presente, lo script non firma, non installa strumenti e produce lo stesso zip di oggi.

Non acquistare nulla da questo repository: il certificato lo ottieni tu, oppure usi un account Azure che hai già.

## Certificato PFX (file)

Un certificato di code signing in formato `.pfx`. Può essere:

- un certificato emesso da una CA pubblica (i browser e SmartScreen si fidano dopo la reputazione);
- per prove locali, un certificato autofirmato creato sul tuo PC. Windows mostrerà un avviso finché non lo metti in **Autorità di certificazione radice attendibili** o in **Autori attendibili**.

Esempio di certificato di prova, solo sulla tua macchina, non nel workflow:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=NOREA Local Test" `
  -KeyUsage DigitalSignature -FriendlyName "NOREA Local Test" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
$pwd = ConvertTo-SecureString "norea-test" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "$env:USERPROFILE\norea-test.pfx" -Password $pwd
```

Poi, in locale:

```powershell
$env:NOREA_CODESIGN_PFX_PATH = "$env:USERPROFILE\norea-test.pfx"
$env:NOREA_CODESIGN_PFX_PASSWORD = "norea-test"
.\crea_pacchetto_portatile.ps1
```

## Segreti GitHub (repository → Settings → Secrets and variables → Actions)

Imposta **uno** dei due gruppi. Non serve impostarli tutti e due. Se ci sono entrambi, viene usato il PFX.

| Segreto | Contenuto |
|---|---|
| `NOREA_CODESIGN_PFX_BASE64` | Il file `.pfx` codificato in Base64 (`[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))`) |
| `NOREA_CODESIGN_PFX_PASSWORD` | Password del PFX. Obbligatoria solo se il file è protetto; può restare assente se il PFX non ha password |
| `NOREA_AZURE_SIGNING_ENDPOINT` | Endpoint HTTPS dell'account Azure Artifact Signing, ad esempio `https://wus2.codesigning.azure.net` |
| `NOREA_AZURE_SIGNING_ACCOUNT` | Nome dell'account di firma |
| `NOREA_AZURE_SIGNING_PROFILE` | Nome del profilo certificato |
| `NOREA_AZURE_TENANT_ID` | Tenant dell'identità usata dal workflow |
| `NOREA_AZURE_CLIENT_ID` | Application (client) id |
| `NOREA_AZURE_CLIENT_SECRET` | Segreto del service principal |

Azure Artifact Signing (il servizio prima chiamato Trusted Signing) è a pagamento sul tuo account Azure: questo repository non lo attiva e non lo acquista. Il service principal deve avere il ruolo **Artifact Signing Certificate Profile Signer** sul profilo. Lo strumento `sign` 1.1.5 viene installato nel runner solo quando questi segreti ci sono, e usa `DefaultAzureCredential` con le tre variabili `AZURE_*` ricavate dai segreti.

Una configurazione a metà (per esempio la password senza il PFX, o solo l'endpoint Azure) fa fallire il job, così non si pubblica un pacchetto creduto firmato. Nessun segreto: il job prosegue come oggi.

La firma usa SHA-256 e la marca temporale `http://timestamp.digicert.com`. Vengono firmati gli `.exe` nella cartella del pacchetto, prima dello zip.

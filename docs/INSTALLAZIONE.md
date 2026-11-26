# Installazione

Lo zip portatile resta il pacchetto principale. Accanto ci sono tre modi per installare, nessuno dei quali richiede di comprare un certificato per essere generato.

## Per l'utente, dallo zip

Scompatta `NOREA_Suite_Portable.zip` ed esegui `installa_suite.ps1`. Copia i file in `%LocalAppData%\Programs\NOREA` e crea il collegamento **NOREA Launcher** nel menu Start. Non chiede l'amministratore.

## MSIX

`crea_installer.ps1` (e il workflow di release) costruisce un MSIX con `makeappx` se il Windows SDK è installato. I runner `windows-latest` ce l'hanno.

- Senza segreti di firma il file si chiama `NOREA_Suite_unsigned.msix`. Windows non lo installa con un doppio clic: un MSIX deve essere firmato.
- Con i segreti di [FIRMA_AUTHENTICODE.md](FIRMA_AUTHENTICODE.md) il workflow firma il pacchetto e lo pubblica come `NOREA_Suite.msix`. Il campo `Publisher` del manifesto viene preso dal soggetto del certificato, altrimenti `makeappx` e la firma non coincidono.
- Per provarlo sul tuo PC senza un certificato pubblico:

```powershell
.\crea_pacchetto_portatile.ps1
.\crea_installer.ps1 -PortableDir .\norea_portable -ZipPath .\NOREA_Suite_Portable.zip -SelfSigned
```

`-SelfSigned` crea un certificato `CN=NOREA Local Test` (solo in questa esecuzione), firma l'MSIX e lascia `installer_out\NOREA_Suite.msix`. Prima di `Add-AppxPackage` importa quel certificato in **Autori attendibili** dell'utente corrente. Il PFX di prova usa la password `norea-test` e viene cancellato a fine script; il certificato resta nello store `CurrentUser\My` finché non lo rimuovi.

L'app MSIX dipende dal runtime .NET 10 Desktop già installato sulla macchina, come lo zip leggero. Dentro il pacchetto l'eseguibile di avvio è `NoreaLauncher.exe`.

## winget

Il workflow scrive `installer_out/ChristianRobecchi.NOREA.yaml`: un manifest singleton che punta allo zip della release, con hash SHA-256 calcolato su quel file, e comandi portatili `norea-launcher`, `norea` e `notepadrea`.

Questo file **non** compare in `winget search` da solo. Va proposto con una pull request al repository [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), di solito sotto `manifests/c/ChristianRobecchi/NOREA/<versione>/`. L'identificativo `ChristianRobecchi.NOREA` si può cambiare in quella pull request se il team winget ne chiede un altro. Non serve un certificato per lo zip portatile.

In CI, `crea_pacchetto_portatile.ps1` rigenera il manifest con `-SkipMsix`, così lo script viene eseguito a ogni pull request senza dipendere da `makeappx`. Il MSIX vero si crea nel workflow di release.

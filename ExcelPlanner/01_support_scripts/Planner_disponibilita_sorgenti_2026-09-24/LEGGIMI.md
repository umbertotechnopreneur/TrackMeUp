# Archivio del lavoro sul planner — 24 settembre 2026

Qui sono salvati **tutti i 57 file** della cartella temporanea di lavoro: script `.mjs`, `.ps1` e `.py`, dati JSON, analisi, immagini, PDF e copie usate per i controlli. Sono inclusi anche i tentativi intermedi, non solo quelli riusciti. Le immagini iniziali dell’utente ancora presenti in Temp sono conservate in `Riferimenti_utente`.

Le copie sono state verificate con **SHA-256**. `ARCHIVE-MANIFEST.json` elenca i 57 file e le impronte; i riferimenti dell’utente hanno un manifest separato. Non è stato cancellato nulla da Temp e gli Excel originali, ora in [Originali](../Originali/), non sono stati modificati. I percorsi nei manifest documentano la posizione al momento dell’archiviazione, prima del riordino in `01_support_scripts`.

## Script principali

- `prepare-data.ps1`: dati iniziali e download di festività/fasi lunari.
- `export-worldclocks.py` e `use-worldclocks.ps1`: lettura del nostro DB e generazione dei periodi dei fusi.
- `build-planner.mjs`: creazione del workbook e dei CSV.
- `finalize-planner.ps1`: connessioni Power Query, nomi e configurazione Excel.
- `fix-print.ps1`: configurazione finale e verifica del formato A4.
- `check-planner.ps1`: controlli su una copia temporanea, senza modificare il workbook consegnato.
- `inspect*` e `layout-help.mjs`: strumenti usati durante l’analisi.
- `archive-work.ps1`: copia e verifica di questo archivio.

## Prima di rieseguirli

**È una copia fedele di lavoro, non un pacchetto portabile pronto da lanciare.** Gli script mantengono i percorsi assoluti originali, inclusi Temp, il repository e i runtime Codex. Vanno adeguati prima di una nuova esecuzione. Il generatore può sovrascrivere workbook e CSV se usato con l’opzione di sostituzione: non lanciarlo sui dati modificati a mano.

L’unico collegamento escluso è `node_modules`: puntava alle librerie installate nel runtime Codex, fuori da Temp. Il suo percorso è registrato nel manifest; non sono state duplicate le librerie globali in OneDrive. Servono Node con `@oai/artifact-tool`, PowerShell 7, Python e, per i passaggi nativi, Excel desktop.

Il risultato da usare è [Planner_Disponibilita_A4.xlsx](../../Planner_Disponibilita_A4.xlsx), direttamente in **Excel prototypes**, accanto a **Calendar data**. Le anteprime in questo archivio possono essere precedenti alle correzioni finali.

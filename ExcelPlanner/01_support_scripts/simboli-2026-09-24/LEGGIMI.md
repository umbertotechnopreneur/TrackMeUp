# Simboli Unicode — 24 settembre 2026

- Bandiere accanto alle città in Planner ed Estesa: seguono `CountryCode` di `ZoneCatalog`, non il calendario festivo.
- Otto fasi lunari, con simbolo accanto alla fase corrente e alla prossima.
- Meteo: simbolo ricavato dalla descrizione già importata; una descrizione non riconosciuta usa il termometro, un dato assente non inventa condizioni.
- Solo caratteri Unicode: nessun file immagine, font installato, chiamata API o modifica ai CSV. Font originali conservati.
- Nell'anteprima nativa attuale le bandiere si leggono come sigle (VN, IT, ecc.). I caratteri regional-indicator sono presenti; la resa dipende dal font e dal motore di Excel. Le lune sono memorizzate come otto caratteri distinti.
- Impaginazione originale conservata: Planner una pagina, Estesa due. L'impostazione del file è A4; l'esportazione PDF nella configurazione stampante corrente produce Letter sia prima sia dopo la modifica. Nessuna impostazione globale di stampa modificata.

`build-symbols.mjs` prepara le 71 modifiche. `apply-symbols.ps1` le applica a una copia, verifica formule, query e aggiornamenti dinamici. `verify-layout.py` confronta contenuti e stampa. La pubblicazione usa `apply-symbols.ps1 -Publish` dopo la revisione visiva, con controllo dell'hash e backup.

I supporti restano qui. Il file d'uso resta `../../Planner_Disponibilita_A4.xlsx`.

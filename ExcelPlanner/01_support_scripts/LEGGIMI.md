# Planner disponibilità

Questa è la copia versionata del prototipo. Avvia `calendari-2026-09-24/Apri-Planner.ps1`: apre una copia in `.local/` ignorata da Git e configura i CSV. Il modello nella cartella superiore resta privo di chiavi e percorsi personali.

## Cosa abbiamo

- **Copertina** informativa A4; **Parametri** per gli input; **Planner** su una A4 orizzontale; **Estesa** su due A4.
- Sette colonne indipendenti: città/fuso, attività locale, riposi settimanali e calendario. Margini comuni prima/dopo: bianco disponibile, rosa con riserva, rosso non disponibile.
- **Eccezioni** per ferie, chiusure e recuperi personali, con precedenza sui calendari. `Attivita` ripristina gli orari della colonna, non apre tutte le 24 ore.
- **206 città**, coordinate dal DB WorldClocks; **670 periodi DST** per il 2026–2028. Nessuna modifica al repository o al DB.
- Calendari nazionali 2026–2028 per **Cina continentale, India, Corea del Sud, Giappone, Bangladesh, Italia, Francia, Norvegia, Svezia, USA e Canada**. Conservati i dati precedenti di Inghilterra e Galles. Totale: **542 righe**, compresi sei recuperi lavorativi cinesi nel 2026.
- Santi e ricorrenze: **211 voci su 191 date**, solo informazioni. Luna: dati USNO 2025–2029.
- **Meteo OpenWeather** per le città selezionate: una prima lettura delle sette città è già nei dati. In **Parametri B32** trovi il campo chiave, lasciato vuoto.

## Partire in un minuto

1. In **Parametri** imposta data, ora, margini e le sette colonne. Nei giorni settimanali: `1 = riposo`, `0 = attività`.
2. In **Eccezioni** inserisci il numero della colonna, date locali incluse, `Riposo` o `Attivita`, e `Attiva = 1`.
3. **Dati → Aggiorna tutto** ricarica i dati. Chiave meteo vuota: legge `Meteo.csv`. Chiave compilata: interroga OpenWeather. Nessun refresh automatico in apertura.
4. Stampa il solo foglio **Planner** o **Estesa**, non l'intera cartella. Il PDF è una fotografia, non si aggiorna da solo.

La chiave in una cella viene salvata in chiaro: non condividere quella copia. La chiave demo fornita non è inserita nell'Excel. Il meteo è **attuale**, non una previsione riferita alla data del planner; sotto ogni città è riportata l'ora UTC dell'osservazione.

## Cosa non abbiamo ancora

- Calendari regionali, aziendali e patroni applicati automaticamente. USA/Canada sono una base federale; l'applicabilità al singolo datore di lavoro va controllata. Date stimate e futuri ponti/recuperi possono cambiare: vedi `Status` in **Da_file**.
- Vietnam e Bielorussia restano predisposti ma non caricati. Il santorale è una selezione incompleta, non un calendario liturgico annuale.
- Mini-tool, embed Excel, IPC e skyline non sono implementati. Emoji del meteo, fasi lunari e caratteri delle bandiere sono presenti; la resa dipende da Excel e dai font.

## File da tenere insieme

Servono il **workbook** e **Calendar data** con sei CSV. Il launcher aggiorna **Parametri B25** nella copia locale. Gli script, le fonti e le licenze stanno in **01_support_scripts**. Il piano privato, i PDF di verifica e i backup rimangono in Obsidian.

Per questo prototipo è autorizzato Fast Combine: lo script di apertura in `calendari-2026-09-24` lo abilita solo nella sessione di questo workbook, senza modificare Excel globalmente. La modalità API può richiedere l'accesso **Anonimo** all'origine `https://api.openweathermap.org`. Le prove native e i limiti effettivamente verificati sono nel resoconto della cartella del round.

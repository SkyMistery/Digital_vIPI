# Deploy vIPI su Render (free) + Neon (Postgres free)

Nessuna carta di credito. `https://<nome>.onrender.com` con TLS automatico. Difetto del piano free:
il servizio **dorme dopo 15 min** di inattività → primo accesso lento (~1 min), poi normale.

Lo schema DB su Postgres si crea da modello (`EnsureCreated`) all'avvio: DB fresco, nessuna migrazione.

---

## 1. Database Neon (Postgres)

1. Registrati su https://neon.tech (login Google/GitHub, no carta).
2. Crea un progetto → un database (region EU, es. Frankfurt).
3. Dashboard → **Connection string** → scegli il formato **`.NET`** (Npgsql). Sarà tipo:
   ```
   Host=ep-xxx.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=xxx;SSL Mode=Require;Trust Server Certificate=true
   ```
   Copiala: è il valore di `ConnectionStrings__Vipi`.

## 2. Web service su Render

1. Registrati su https://render.com (login GitHub, no carta).
2. **New → Blueprint** → collega il repo `SkyMistery/Digital_vIPI`. Render legge `render.yaml`.
   (In alternativa **New → Web Service**, runtime **Docker**, lascia che usi il `Dockerfile`.)
3. Alla creazione ti chiede i valori dei segreti `sync:false`. Inseriscili:
   | Chiave | Valore |
   |--------|--------|
   | `ConnectionStrings__Vipi` | stringa Neon del punto 1 |
   | `VipiAuth__ClientId` / `VipiAuth__ClientSecret` | client OIDC IVAO |
   | `Ivao__ClientId` / `Ivao__ClientSecret` | token app IVAO (polling tracker) |
4. **Create** → Render builda il Docker e pubblica. Segna l'URL `https://<nome>.onrender.com`.

## 3. Redirect OIDC su IVAO

Portale sviluppatori IVAO → la tua app → redirect URI (identici all'URL Render):
```
https://<nome>.onrender.com/signin-oidc
https://<nome>.onrender.com/signout-callback-oidc
```

Apri l'URL → online. Il primo accesso dopo inattività è lento (cold start), è normale sul piano free.

---

## Note

- **DB condiviso e persistente**: vive su Neon, non su Render. Sopravvive a redeploy e allo sleep del web service.
- **Schema**: creato da `EnsureCreated` al primo avvio. Se cambi il modello dati e vuoi ricreare, azzera il
  database su Neon (drop schema) e riavvia: NON ci sono migrazioni incrementali su Postgres in questo setup.
- **Dev locale invariato**: senza `Persistence__Provider` resta SQLite con le migrazioni versionate.
- **Tenere sveglio** (opzionale): un ping esterno ogni ~10 min (es. cron-job.org su `/vsop`) evita lo sleep.

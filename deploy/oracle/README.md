# Deploy vIPI su Oracle Cloud Always Free

Stack: VM Ubuntu (Ampere ARM, gratis a vita) + Docker Compose (app .NET + Caddy per HTTPS automatico) +
dominio gratuito DuckDNS. Risultato: `https://<tuonome>.duckdns.org` sempre attivo, con login IVAO.

---

## 1. Account + VM

1. Registrati su https://www.oracle.com/cloud/free/ (carta richiesta solo per verifica, **non addebitata**;
   scegli l'account "Always Free").
2. Console → **Compute → Instances → Create instance**:
   - **Image**: Ubuntu 22.04.
   - **Shape**: `VM.Standard.A1.Flex` (ARM Ampere) — es. 1 OCPU / 6 GB (dentro il free; puoi salire a 4/24).
   - **SSH keys**: carica la tua chiave pubblica (o generane una; ti serve per entrare).
   - Crea. Segna l'**IP pubblico**.

## 2. Apri le porte 80/443

Due livelli di firewall, servono entrambi.

**a) VCN (console Oracle)**: Networking → VCN → Subnet → Security List → **Add Ingress Rules**:
   - Source `0.0.0.0/0`, TCP, dest port **80**.
   - Source `0.0.0.0/0`, TCP, dest port **443**.

**b) Firewall dentro la VM** (l'immagine Ubuntu di Oracle blocca di default). Via SSH:
```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

## 3. Dominio gratuito (DuckDNS)

Login OIDC richiede HTTPS con dominio valido; l'IP nudo non basta.
1. Vai su https://www.duckdns.org (login con Google/GitHub).
2. Crea un sottodominio, es. `vipitest` → dominio `vipitest.duckdns.org`.
3. Nel campo **current ip** metti l'IP pubblico della VM → **update**.

## 4. Installa Docker sulla VM

SSH nella VM (`ssh ubuntu@<IP>`), poi:
```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker   # applica il gruppo senza rilogin
```

## 5. Clona, configura, avvia

```bash
git clone https://github.com/SkyMistery/Digital_vIPI.git
cd Digital_vIPI
cp .env.example .env
nano .env        # riempi SITE_ADDRESS + i 4 segreti IVAO + ADMIN_STAFF_PATTERN
docker compose up -d --build
```
Il primo build (.NET su ARM) dura qualche minuto. Caddy prende il certificato Let's Encrypt da solo
(serve che la porta 80 sia raggiungibile e il DNS DuckDNS punti già alla VM).

## 6. Registra i redirect OIDC su IVAO

Portale sviluppatori IVAO → la tua app → redirect URI:
```
https://<SITE_ADDRESS>/signin-oidc
https://<SITE_ADDRESS>/signout-callback-oidc
```
Devono combaciare esatti con `SITE_ADDRESS`, altrimenti il login fallisce.

Apri `https://<SITE_ADDRESS>` → sei online.

---

## Gestione

| Azione | Comando (nella cartella del repo, sulla VM) |
|--------|---------------------------------------------|
| Log app | `docker compose logs -f app` |
| Riavvia | `docker compose restart` |
| Aggiorna codice | `git pull && docker compose up -d --build` |
| Backup DB | `docker compose cp app:/app/data/vipi.db ./vipi-backup.db` |
| Stop | `docker compose down` (il volume `vipi_data` resta) |

**Il DB SQLite vive nel volume `vipi_data`**: sopravvive a restart, rebuild e `down`. Fai backup periodici
con il comando qui sopra. Un solo container app = SQLite senza conflitti (Blazor Server è mono-istanza).

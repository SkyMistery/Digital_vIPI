@echo off
rem Aurora Sector Lab, versione di prova per gli AOD (carta docs/feature/2026-11-03-kit-di-prova-per-gli-aod.md).
rem I dati del Lab stanno in "dati" qui accanto, il sector e l'AIP espanso in "lavoro": per ricominciare da zero
rem chiudi il Lab e cancella le due cartelle.
start "" "%~dp0app\VipiSectorLab.exe" --dati "%~dp0dati" --lavoro-proposto "%~dp0lavoro" --giro-di-prova

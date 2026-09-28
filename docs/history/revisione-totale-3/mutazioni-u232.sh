#!/bin/bash
# U-232 (revisione 3, S39): quattro mutazioni, una alla volta; ognuna deve far diventare rossa la suite nuova.
cd "$(dirname "$0")/../../.."
S=$(mktemp -d)


prova() {  # $1 file, $2 script python di mutazione, $3 filtro
  cp "$1" $S/bak
  py -c "$2"
  git diff --stat -- "$1" | tail -1
  dotnet test tests/Vipi.Infrastructure.Tests --filter "$3" -f net10.0 2>&1 | grep -E "error|Passed!|Failed!" | head -3
  cp $S/bak "$1"
  git diff --quiet -- "$1" && echo "  ripristinato"
}

F=src/Vipi.Application/Content/AgreementEditingService.cs
echo "== 1 DeleteClauseAsync senza StrutturaAsync"
prova $F "
p='$F'; s=open(p,encoding='utf-8-sig').read(); raw=open(p,'rb').read()
i=s.index('public async Task DeleteClauseAsync'); j=s.index('await StrutturaAsync(ct);', i)
s=s[:j]+'/*mut*/'+s[j+len('await StrutturaAsync(ct);'):]
open(p,'wb').write((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+s.encode('utf-8'))" "FullyQualifiedName~PorteTutteLeScritture"

F=src/Vipi.Application/Content/AppDocumentService.cs
echo "== 2 SaveRegulatedAsync dell'APP con EnsureAsync"
prova $F "
p='$F'; s=open(p,encoding='utf-8-sig').read(); raw=open(p,'rb').read()
i=s.index('public async Task SaveRegulatedAsync'); j=s.index('EnsureWritableAsync(', i)
s=s[:j]+'EnsureAsync('+s[j+len('EnsureWritableAsync('):]
open(p,'wb').write((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+s.encode('utf-8'))" "FullyQualifiedName~LockDelleScrittureStrutturate"

F=src/Vipi.Infrastructure/Persistence/EfGlossaryStore.cs
echo "== 3 glossario con la soglia a DivisionStaff"
prova $F "
p='$F'; s=open(p,encoding='utf-8-sig').read(); raw=open(p,'rb').read()
s=s.replace('EnsureAtLeast(VipiRole.Editor)','EnsureAtLeast(VipiRole.DivisionStaff)')
open(p,'wb').write((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+s.encode('utf-8'))" "FullyQualifiedName~PorteDelleAnagrafiche"

F=$(grep -rln "class EfStatsSettingsStore" src)
echo "== 4 classifica con la soglia a Editor"
prova $F "
p='$F'; s=open(p,encoding='utf-8-sig').read(); raw=open(p,'rb').read()
s=s.replace('EnsureAtLeast(VipiRole.DivisionStaff)','EnsureAtLeast(VipiRole.Editor)',1)
open(p,'wb').write((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+s.encode('utf-8'))" "FullyQualifiedName~PorteDelleAnagrafiche"

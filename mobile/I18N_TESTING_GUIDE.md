# Bilingual Testing Guide - SafeSignal Mobile App

## Quick Start Testing

### 1. Verify App Launches

```bash
cd mobile
npm start
# or
./scripts/start-with-auto-ip.sh
```

### 2. Test Language Selector

#### Steps:

1. Open the app
2. Navigate to **Settings** tab (bottom navigation)
3. Scroll down to find **Language** section (after Appearance, before Data & Sync)
4. Verify you see:
    - **Norwegian** (Norsk) option with language icon
    - **English** option with language icon
    - Checkmark on currently selected language

#### Expected Behavior:

-   Tapping **Norwegian** should:

    -   Update all UI text to Norwegian immediately
    -   Show checkmark next to Norwegian option
    -   Save preference (persists after app restart)

-   Tapping **English** should:
    -   Update all UI text to English immediately
    -   Show checkmark next to English option
    -   Save preference (persists after app restart)

### 3. Screen-by-Screen Testing

#### LoginScreen

**Norwegian Text Check:**

-   "SafeSignal"
-   "NØDVARSLING"
-   "Logg inn med Feide"
-   "Logg inn med BankID"
-   "Eller fortsett med e-post"
-   "E-post"
-   "Passord"
-   "Logg inn"
-   "Bruk biometrisk autentisering"
-   "Kun for autorisert personell"

**English Text Check:**

-   "SafeSignal"
-   "EMERGENCY ALERTS"
-   "Sign in with Feide"
-   "Sign in with BankID"
-   "Or continue with email"
-   "Email"
-   "Password"
-   "Sign In"
-   "Use Biometric Authentication"
-   "For authorized personnel only"

#### HomeScreen

**Norwegian Text Check:**

-   "Velkommen tilbake,"
-   "Velg bygning"
-   "Velg ditt nåværende rom"
-   "Nødvarsel"
-   "Trykk og hold for å utløse et varsel"
-   "NØDSITUASJON"
-   "Varslingsmoduser"
-   "Hørbar varsel" / "Stille varsel" / "Medisinsk nødsituasjon" / "Brannvarsel"
-   "System tilkoblet"

**English Text Check:**

-   "Welcome back,"
-   "Select Building"
-   "Select Your Current Room"
-   "Emergency Alert"
-   "Press and hold to trigger an alert"
-   "EMERGENCY"
-   "Alert Modes"
-   "Audible Alert" / "Silent Alert" / "Medical Emergency" / "Fire Alert"
-   "System Online"

#### SettingsScreen

**Test All Sections:**

1. **Profile Section (Profil / Profile)**

    - Check section header translates
    - User info displays correctly

2. **Security Section (Sikkerhet / Security)**

    - Norwegian: "Fingeravtrykk" / "Ansiktsgjenkjenning" / "Biometrisk"
    - English: "Fingerprint" / "Face ID" / "Biometric"
    - "Krev autentisering for varsler" / "Require Authentication for Alerts"
    - "Autolås" / "Auto-Lock"

3. **Appearance Section (Utseende / Appearance)**

    - "Lys modus" / "Light Mode"
    - "Mørk modus" / "Dark Mode"
    - "System"

4. **Language Section (Språk / Language)** ⭐

    - Section header shows "SPRÅK" (nb) or "LANGUAGE" (en)
    - "Norsk" option with "Norsk" subtitle
    - "Engelsk" / "English" option with "English" subtitle
    - Checkmark on selected language

5. **Data & Sync Section (Data og synkronisering / Data & Sync)**

    - "Synkroniser nå" / "Sync Now"
    - "Synkroniserer..." / "Syncing..."
    - Last sync timestamp in correct locale

6. **About Section (Om / About)**

    - "Versjon" / "Version"
    - "Nødvarslingsystem" / "Emergency Alert System"
    - "© 2025 SafeSignal"
    - "Kun for autorisert personell" / "For authorized personnel only"

7. **Logout Button**
    - "Logg ut" / "Log Out"

#### AlertConfirmationScreen

**Norwegian:**

-   "Bekreft varsel"
-   Alert mode name in Norwegian
-   "Bygning" / "Rom"
-   "Avbryt" / "Send varsel"

**English:**

-   "Confirm Alert"
-   Alert mode name in English
-   "Building" / "Room"
-   "Cancel" / "Send Alert"

#### AlertSuccessScreen

**Norwegian:**

-   "Varsel sendt vellykket"
-   "Nødetater har blitt varslet"
-   "Varseltype" / "Plassering" / "Tid" / "Status"
-   "Aktiv"
-   "Fjern varsel"
-   "Varselhistorikk"
-   "Ferdig"

**English:**

-   "Alert Sent Successfully"
-   "Emergency services have been notified"
-   "Alert Type" / "Location" / "Time" / "Status"
-   "Active"
-   "Clear Alert"
-   "View History"
-   "Done"

#### AlertHistoryScreen

**Norwegian:**

-   "Varselhistorikk"
-   "Ingen tidligere varsler"
-   "Du har ikke sendt noen varsler ennå"
-   Alert mode names in Norwegian
-   "Fjernet" status

**English:**

-   "Alert History"
-   "No Previous Alerts"
-   "You haven't sent any alerts yet"
-   Alert mode names in English
-   "Cleared" status

#### AlertClearanceScreen

**Norwegian:**

-   "Fjern varsel"
-   "Er du sikker på at du vil fjerne dette varselet?"
-   "Ja, fjern varsel"
-   "Avbryt"
-   "Plassering" / "Sendt"
-   "Årsak til fjerning (valgfritt)"

**English:**

-   "Clear Alert"
-   "Are you sure you want to clear this alert?"
-   "Yes, Clear Alert"
-   "Cancel"
-   "Location" / "Sent at"
-   "Clearance Reason (Optional)"

### 4. Alert Dialog Testing

Test all Alert dialogs in both languages:

**Error Dialogs:**

-   Login errors: "Feil" / "Error"
-   Validation messages in correct language

**Confirmation Dialogs:**

-   Logout confirmation: "Bekreft utlogging" / "Confirm Logout"
-   Alert clearance: "Fjern varsel" / "Clear Alert"

**Success Dialogs:**

-   "Suksess" / "Success"
-   Operation completed messages

### 5. Dynamic Content Testing

**Test Interpolation:**

1. Check sync status with count: "Synkroniserer (3 ventende)" / "Syncing (3 pending)"
2. Check last sync time in correct locale
3. Check relative time: "2 timer siden" / "2 hours ago"

**Test Pluralization:**

-   1 item: "1 handling" / "1 action"
-   Multiple items: "5 handlinger" / "5 actions"

### 6. Date/Time Localization

**Test Date Formats:**

-   Norwegian: "5. jan. 2025, 10:30"
-   English: "Jan 5, 2025, 10:30 AM"

**Test Relative Times:**

-   Norwegian: "nå nettopp", "2 minutter siden", "3 timer siden", "2 dager siden"
-   English: "just now", "2 minutes ago", "3 hours ago", "2 days ago"

### 7. Persistence Testing

**Test Language Persistence:**

1. Select Norwegian
2. Close app completely (force quit)
3. Reopen app
4. ✅ App should still be in Norwegian

**Test After Device Restart:**

1. Select English
2. Restart iOS Simulator / Android Emulator
3. Reopen app
4. ✅ App should still be in English

### 8. Device Locale Testing

**Test Initial Language Detection:**

1. Delete app from device/simulator
2. Change device language to Norwegian
3. Install and open app
4. ✅ App should start in Norwegian

**Repeat for English:**

1. Delete app
2. Change device language to English
3. Install and open app
4. ✅ App should start in English

### 9. Edge Cases

**Special Characters:**

-   Verify Norwegian characters display correctly: æ, ø, å
-   Test in: Usernames, building names, room names, alert messages

**Long Text:**

-   Test with long building/room names
-   Verify text doesn't overflow or break layout
-   Check both languages handle long strings gracefully

**Network Errors:**

-   Test error messages during sync failures
-   Verify offline messages in correct language

### 10. Accessibility Testing

**VoiceOver / TalkBack:**

1. Enable VoiceOver (iOS) or TalkBack (Android)
2. Navigate through app in Norwegian
3. ✅ Screen reader should read Norwegian text
4. Switch to English
5. ✅ Screen reader should read English text

## Automated Testing (Future)

### Unit Tests

```typescript
describe('i18n', () => {
    it('should translate to Norwegian', () => {
        changeLanguage('nb');
        expect(t('common.appName')).toBe('SafeSignal');
        expect(t('common.appTagline')).toBe('NØDVARSLING');
    });

    it('should translate to English', () => {
        changeLanguage('en');
        expect(t('common.appName')).toBe('SafeSignal');
        expect(t('common.appTagline')).toBe('EMERGENCY ALERTS');
    });
});
```

### Integration Tests

```typescript
describe('Language Selector', () => {
    it('should change language to Norwegian', async () => {
        await navigateToSettings();
        await tapLanguageOption('Norwegian');
        await waitFor(element(by.text('Innstillinger'))).toBeVisible();
    });
});
```

## Bug Reporting

When reporting translation bugs, include:

1. **Screen**: Which screen has the issue
2. **Language**: Norwegian or English
3. **Expected**: What text should appear
4. **Actual**: What text actually appears
5. **Screenshot**: If possible
6. **Steps to Reproduce**: How to trigger the issue

Example:

```
Screen: Settings
Language: Norwegian
Expected: "Synkroniser nå"
Actual: "Sync Now"
Steps: Open Settings → Scroll to Data & Sync section
```

## Checklist Summary

-   [ ] Language selector visible in Settings
-   [ ] Norwegian option works correctly
-   [ ] English option works correctly
-   [ ] All 7 screens translated correctly
-   [ ] Alert dialogs translated
-   [ ] Error messages translated
-   [ ] Success messages translated
-   [ ] Date/time in correct locale
-   [ ] Language preference persists
-   [ ] Special characters (æ, ø, å) display correctly
-   [ ] Device locale detection works
-   [ ] VoiceOver/TalkBack reads correct language

---

**Testing completed by**: **\*\***\_\_\_**\*\***
**Date**: **\*\***\_\_\_**\*\***
**Issues found**: **\*\***\_\_\_**\*\***

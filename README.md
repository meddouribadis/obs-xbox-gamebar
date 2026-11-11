# 🎮 OBS Controller - Xbox Game Bar Widget

Widget officiel pour la **Xbox Game Bar** (`Win + G`) permettant de piloter **OBS Studio** directement en superposition au-dessus de vos jeux vidéo, sans avoir besoin d'effectuer de Alt+Tab.

---

## ✨ Fonctionnalités

* 🔴 **Contrôle de l'Enregistrement (REC)** : Démarrer, arrêter, mettre en pause et reprendre l'enregistrement avec chronomètre en direct.
* 📡 **Contrôle du Streaming (LIVE)** : Lancer ou couper la diffusion en direct avec chronomètre.
* ⚡ **Sauvegarde instantanée de Clip (Replay Buffer)** : Sauvegardez les X dernières secondes de jeu en un clic (comme ShadowPlay mais via OBS !).
* 📷 **Caméra Virtuelle** : Activer / désactiver la caméra virtuelle OBS (utile pour Discord, etc.).
* 🎬 **Sélecteur de Scènes interactif** : Liste synchronisée en temps réel avec OBS, mise en évidence de la scène active et changement de scène instantané.
* 🎙️ **Mixeur Audio & Mute Rapide** : Visualisation et bascule Mute/Unmute pour le microphone et le son du système.
* 📌 **Mode Épinglé (Pin)** : Grâce à la Xbox Game Bar, vous pouvez épingler le widget dans un coin de votre écran, régler sa transparence et continuer à jouer avec les contrôles toujours sous les yeux.

---

## 🛠️ Prérequis

1. **Windows 10 (2004+) ou Windows 11** avec l'application Xbox Game Bar installée.
2. **OBS Studio 28+** (OBS Studio 33 est déjà installé sur votre machine).
3. **Visual Studio Community** avec la charge de travail :
   * **Développement pour la plateforme Windows universelle (UWP)** *(Universal Windows Platform development)*.

---

## ⚙️ Étape 1 : Activer le serveur WebSocket dans OBS Studio

1. Lancez **OBS Studio**.
2. Dans le menu supérieur, cliquez sur **Outils** > **Paramètres du serveur WebSocket**.
3. Cochez **Activer le serveur WebSocket**.
4. Vérifiez que le port est bien **4455** (port par défaut).
5. *(Optionnel)* Si vous définissez un mot de passe, notez-le pour le renseigner dans le widget.
6. Cliquez sur **Appliquer** puis **OK**.

---

## 🚀 Étape 2 : Compiler et Déployer le Widget

1. Ouvrez la solution `Solution1.slnx` (ou `Solution1.sln`) dans **Visual Studio Community**.
2. Dans la barre d'outils supérieure :
   * Choisissez la configuration : `Debug` ou `Release`.
   * Choisissez l'architecture : `x64` (ou `ARM64` si processeur Snapdragon).
3. Faites un **clic droit** sur le projet `ObsGameBarWidget` dans l'Explorateur de solutions et cliquez sur **Déployer** (ou appuyez simplement sur `F5`).
4. Windows déploie le package et l'enregistre automatiquement auprès du système Xbox Game Bar.

---

## 🔓 Étape 3 : Autoriser la connexion localhost (Loopback)

Sous Windows, les applications UWP fonctionnent dans un conteneur sécurisé (*AppContainer*) qui bloque par défaut les connexions vers `127.0.0.1` (`localhost`).

Pour autoriser le widget à se connecter à OBS sur `localhost:4455`, ouvrez PowerShell et exécutez le script fourni :

```powershell
powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Enable-LoopbackExemption.ps1
```

*(Cette étape n'a besoin d'être effectuée qu'une seule fois).*

---

## 🎮 Étape 4 : Utiliser le widget dans la Game Bar

1. Appuyez sur le raccourci clavier **`Touche Windows + G`** pour afficher la Game Bar.
2. Cliquez sur l'icône **Menu des widgets** (la barre d'icônes tout en haut).
3. Sélectionnez **OBS Controller** (vous pouvez cliquer sur l'étoile pour l'ajouter à vos favoris dans la barre principale).
4. Le widget s'affiche :
   * Si OBS est ouvert, il se connecte automatiquement.
   * Vous pouvez cliquer sur l'icône d'engrenage ⚙️ pour ajuster l'IP, le port ou le mot de passe.
5. **Astuce Pro :** Cliquez sur l'icône de l'**Épingle (📌)** sur la barre de titre du widget : il restera ainsi affiché par-dessus tous vos jeux pendant que vous jouez !

---

## 📁 Structure du Projet

```
Solution1/
├── ObsGameBarWidget/
│   ├── Assets/                       # Logos et icônes officielles du widget
│   ├── Converters/
│   │   └── ValueConverters.cs        # Convertisseurs XAML (Mute, Scènes, Couleurs)
│   ├── Models/
│   │   └── ObsModels.cs              # Modèles de données (Scènes, Audio, États)
│   ├── Services/
│   │   ├── ObsWebSocketService.cs    # Client WebSocket OBS v5 (Auth SHA-256, RPC, Events)
│   │   └── SettingsService.cs        # Persistance des paramètres locaux
│   ├── App.xaml / App.xaml.cs        # Activation de protocole ms-gamebarwidget
│   ├── MainPage.xaml / MainPage.xaml.cs # Interface XAML Fluent & Logique Game Bar
│   ├── Package.appxmanifest          # Déclaration de l'extension Game Bar
│   ├── Enable-LoopbackExemption.ps1  # Script d'exemption loopback UWP
│   └── ObsGameBarWidget.csproj       # Fichier projet UWP C#
├── Solution1.slnx                    # Solution Visual Studio vNext
└── Solution1.sln                     # Solution Visual Studio standard
```

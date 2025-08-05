# Intro
This is a popular shedding-type card game in Moldova that I couldn't find anywhere available to play online. The common names for the game are "President", "Chairman", or "P*****". The game is ready to play in beta version.

## How to start the game

Go to Build folder. If you need Android .apk installer, then just go further to Build/Android and download the file. If you want to play it on Linux, then download all the files from Build/Linux and then run presidentPZ.x86_64 executable.

## Main rules:

The goal of the game is to get rid of all cards at hand. By default, everyone can discard one card from their hand whenever its their turn. By default, you can discard only a card either of the same rank or value as the card at the top of the discard pile. If you none of your cards can be discarded or you prefer not to discard (i.e. to save power cards), then you have to draw a card and decide again to play or pass. Many cards have special powers. If you discard 7, then the next person draws automatically two cards from the face-down pile and skips their turn. If you discard 6, then the previous person draws one. If you discard 9 diamonds, then the next person draws five cards. If you discard an ace, then next person skips a turn. If you discard an 8, then its again your turn and any card irrespective of its rank/value can be discarded next. And finally, jack can be discarded irrespective of the rank/value of the top card in the discard pile. More than that, a player discarding jack decides on the value of the next card and the next player has to follow that value (or alternatively, play jack since it can be put on any card).

Even though the goal of the game is to toss all you cards away, the game ends only if your last played card is either jack or queen and you have not cards left at hand.

When the game ends, that's considered the end of the round, and the players with cards at hand get their values summed up (jack = 2 points, queen = 3 points, ace = 11 points and so on). Exception, if a lost player has only one card at hand which is either jack or queen, that is punished with 20 or 30 poins, respectively. On the other hand, a winner gets his points decreased. If they end the game with jack, that is -20 points, and queen is -30 points.

## Technologies Used

Engine: Unity
Language: C#
Networking: Unity Netcode for GameObjects
Online Services: Unity Gaming Services (Authentication, Relay, Lobby)
Localization: Unity Localization Package
Version Control: Git & Git LFS

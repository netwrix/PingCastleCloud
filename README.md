# Ping Castle Cloud

## Introduction

Ping Castle Cloud is a tool designed to assess quickly the AzureAD security level with a methodology based on risk assessment and a maturity framework.
It does not aim at a perfect evaluation but rather as an efficiency compromise.
It is inspired from the [Ping Castle project](https://pingcastle.com/)

```
  \==--O___      PingCastle Cloud (Version 1.0.0.0     17/07/2022 18:58:40)
   \  / \  ¨¨>   Get Active Directory Security at 80% in 20% of the time
    \/   \ ,'    End of support: 31/07/2023
     O¨---O
      \ ,'       Vincent LE TOUX (contact@pingcastle.com)
       v         twitter: @mysmartlogon       https://www.pingcastle.com
What do you want to do?
=======================
Using interactive mode.
Do not forget that there are other command line switches like --help that you can use
  1-healthcheck  -Score the risk of a domain
  2-exportasguest-Export users and group as a Guest
  3-advanced     -Open the advanced menu
  0-Exit

```

Check https://www.pingcastle.com for the documentation and methodology

## Build

PingCastle is a c# project which can be build from Visual Studio 2019

## Support & lifecycle

For support requests, you should contact support@pingcastle.com
The support for the basic edition is made on a best effort basis and fixes delivered when a new version is delivered.

The Basic Edition of PingCastleCloud is released every 6 months (January, August) and this repository is updated at each release.

If you need changes, please contact contact@pingcastle.com for support packages.

## License

PingCastleCloud source code is licensed under a proprietary license and the Non-Profit Open Software License ("Non-Profit OSL") 3.0.

Except if a license is purchased, you are not allowed to make any profit from this source code.
To be more specific:
* It is allowed to run PingCastle without purchasing any license on for profit companies if the company itself (or its ITSM provider) run it.
* To build services based on PingCastle AND earning money from that, you MUST purchase a license.

Ping Castle uses the following Open source components:

* [Bootstrap](https://getbootstrap.com/) licensed under the [MIT license](https://tldrlegal.com/license/mit-license)
* [JQuery](https://jquery.org) licensed under the [MIT license](https://tldrlegal.com/license/mit-license)
* [vis.js](http://visjs.org/) licensed under the [MIT license](https://tldrlegal.com/license/mit-license)

## Author

Author: Vincent LE TOUX

You can contact me at vincent.letoux@gmail.com





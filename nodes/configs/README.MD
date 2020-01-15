# Configs

## Protocol Magic
```11452115```

## Validators
```
020dda08407ee4f8f88dc13bdbaff77d49f176012071311797e0ff04d9a85755a2
0209be2d8c0c330ad935e8b9e0717762d5d240044db6a12ece8134e91634c9c9af
020f8d760555edd8f90f3149c0a946324da28bdca71a7f99b609d290fb5902ba0c
0385bb7f54f0ca3096cd27776e86a53159e39d4b359a941ea0621a3a1ab628c56e
03e1475fd5bbf28c7c5d2cd6b274104c8e36aae6b7da29011b31e45b25c3cd4357
0294eb17c0cea918d1355650e3d00777adc075a0bad1dead41d74e6b5ab450dd80
036ef783e4dce4b4b128d4674b29739614d0599991c88ee126263004c9f394b110
```

## Consensus Multisig Address

_**NMY2uDhdgLRGjBWYayGq91snsd5VqrsqAm**_

```
import multisigaddress 4 020dda08407ee4f8f88dc13bdbaff77d49f176012071311797e0ff04d9a85755a2 0209be2d8c0c330ad935e8b9e0717762d5d240044db6a12ece8134e91634c9c9af 020f8d760555edd8f90f3149c0a946324da28bdca71a7f99b609d290fb5902ba0c 0385bb7f54f0ca3096cd27776e86a53159e39d4b359a941ea0621a3a1ab628c56e 03e1475fd5bbf28c7c5d2cd6b274104c8e36aae6b7da29011b31e45b25c3cd4357 0294eb17c0cea918d1355650e3d00777adc075a0bad1dead41d74e6b5ab450dd80 036ef783e4dce4b4b128d4674b29739614d0599991c88ee126263004c9f394b110
```

## Distribute initial consensus funds
```
send neo Nj9W8R4dXbsyCTUfmmCgnTnrT8hCKoXZPu 100000000
sign {"type":"Neo.Network.P2P.Payloads.Transaction".....}
 ... (x4)
relay {"type":"Neo.Network.P2P.Payloads.Transaction".....}

send gas Nj9W8R4dXbsyCTUfmmCgnTnrT8hCKoXZPu 20000000
sign {"type":"Neo.Network.P2P.Payloads.Transaction".....}
 ... (x4)
relay {"type":"Neo.Network.P2P.Payloads.Transaction".....}
```
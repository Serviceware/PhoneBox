# PhoneBox
Call me on my tele TAPI.

[![Build Status](https://img.shields.io/azure-devops/build/serviceware/phonebox/2/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=2&branchName=main) [![Test Status](https://img.shields.io/azure-devops/tests/serviceware/phonebox/2/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=2&branchName=main) [![Code coverage](https://img.shields.io/azure-devops/coverage/serviceware/phonebox/2/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=2&branchName=main)

## Packages
| Package | NuGet |
| - | - |
| [PhoneBox.Server](https://www.nuget.org/packages/PhoneBox.Server) | [![PhoneBox.Server](https://img.shields.io/nuget/v/PhoneBox.Server.svg)](https://www.nuget.org/packages/PhoneBox.Server) |

## Authorization
Keycloak requirements:
1. Create client 'phone-box'
   - Set 'Access Type' to 'bearer-only'
   - Add role 'reader'
2. Add 'phone-box' to client that connects to phone-box
   - Create mapper 'audience'
   - Set 'Mapper Type' to 'Audience'
   - Set 'Included Client Audience' to 'phone-box'
   - Set 'Add to access token' to 'ON'
   - Add builtin mapper 'phone number'
   - Set Scopes->'Full Scope Allowed' to 'OFF'
   - Add 'reader' client role under Scope->Client Roles->'phone box'->Available Roles->'reader'->Add selected
# PhoneBox
[![Build Status](https://img.shields.io/azure-devops/build/serviceware/phonebox/4/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=4&branchName=main) [![Test Status](https://img.shields.io/azure-devops/tests/serviceware/phonebox/4/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=4&branchName=main) [![Code coverage](https://img.shields.io/azure-devops/coverage/serviceware/phonebox/4/main)](https://dev.azure.com/serviceware/PhoneBox/_build/latest?definitionId=4&branchName=main)

## Architecture
![](/docs/Phone%20System%20Integration.png)
![](/docs/Incoming%20Call%20Overview.png)

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
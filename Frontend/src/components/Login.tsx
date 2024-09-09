import React, { useState } from 'react';
import { Text, Button } from "@fluentui/react-components";
import { loginRequest } from '../authentication/MsalConfiguration';

export const LoginButton = (props :any) => {
    
    
    return (
        <div style={{
            display: "grid",
            gridTemplateRows: "30px 36px", 
            padding: "24px"}}>
              <Text>
                Debe iniciar sesión para continuar
              </Text>
                <div>                          
                  <Button onClick={() =>{
                    const msalInstance = props.msalContext.instance;
                    msalInstance.loginPopup(loginRequest).catch((err: any) => {
                      console.error(err);
                    });
                }}>Iniciar sesión con sus credenciales</Button>                    
              </div>                    
        </div>             
    );
};


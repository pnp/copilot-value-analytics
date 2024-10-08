import React, { PropsWithChildren } from 'react';

import { Layout } from './components/app/Layout';
import { Dashboard } from './pages/Dashboard/Dashboard';
import { LoginRedirect } from './pages/Login/LoginRedirect';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from "@azure/msal-react";
import { Redirect, Route } from "react-router-dom";
import { BaseApiLoader } from './api/ApiLoader';
import { SurveyManagerPage } from './pages/SurveyEdit/SurveyManagerPage';
import { FluentProvider, teamsLightTheme } from '@fluentui/react-components';
import { TriggersPage } from './pages/Triggers/TriggersPage';

export const AppMain : React.FC<PropsWithChildren<{apiLoader?: BaseApiLoader}>> = (props) => {

    return (
        <FluentProvider
        theme={teamsLightTheme}
      >
            {props.apiLoader ?
                (
                    <Layout apiLoader={props.apiLoader}>
                        <AuthenticatedTemplate>
                            <Route exact path="/">
                                <Redirect to="/tabhome" />
                            </Route>
                            <Route exact path='/tabhome' render={() => <Dashboard loader={props.apiLoader} />} />
                            <Route exact path='/surveyedit' render={() => <SurveyManagerPage loader={props.apiLoader} />} />
                            <Route exact path='/triggers' render={() => <TriggersPage loader={props.apiLoader} />} />
                        </AuthenticatedTemplate>
                    </Layout>
                )
                :
                (
                    <Layout apiLoader={props.apiLoader}>
                        <UnauthenticatedTemplate>
                            <Route exact path='/' component={LoginRedirect} />
                        </UnauthenticatedTemplate>
                        <AuthenticatedTemplate>
                            <p>Loading access token for API...</p>
                        </AuthenticatedTemplate>
                    </Layout>
                )}
        </FluentProvider>
    );

}

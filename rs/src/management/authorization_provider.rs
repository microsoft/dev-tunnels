use std::{
    error::Error,
    future::{self, Future},
    pin::Pin,
};

use super::Authorization;

type ReturnValue<'a> = Pin<Box<dyn Future<Output = Result<Authorization, Box<dyn Error>>> + 'a>>;

pub trait AuthorizationProvider {
    fn get_authorization(&self) -> ReturnValue<'_>;
}

pub struct StaticAuthorization(pub Authorization);

impl AuthorizationProvider for StaticAuthorization {
    fn get_authorization(&self) -> ReturnValue<'_> {
        Box::pin(future::ready(Ok(self.0.clone())))
    }
}

pub struct DelegatedAuthorization<F, Fut>(pub F)
where
    F: Fn() -> Fut,
    Fut: Future<Output = Result<Authorization, Box<dyn Error>>>;

impl<F, Fut> AuthorizationProvider for DelegatedAuthorization<F, Fut>
where
    F: Fn() -> Fut,
    Fut: Future<Output = Result<Authorization, Box<dyn Error>>>,
{
    fn get_authorization(&self) -> ReturnValue<'_> {
        Box::pin((self.0)())
    }
}

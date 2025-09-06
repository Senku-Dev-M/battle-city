module.exports = {
  generateUser,
  generateRoom,
};

function generateUser(userContext, events, done) {
  const id = `${Date.now()}_${Math.floor(Math.random() * 100000)}`;
  userContext.vars.username = `artuser_${id}`;
  userContext.vars.email = `artuser_${id}@example.com`;
  userContext.vars.password = 'Password123!';
  return done();
}

function generateRoom(userContext, events, done) {
  userContext.vars.roomName = `Room_${Date.now()}_${Math.floor(Math.random() * 100000)}`;
  return done();
}
